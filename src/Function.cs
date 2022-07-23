using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Octokit;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace orgbranchprotection;

public class Function
{
    private static IAmazonSimpleNotificationService? client;
    private static JwtBuilder? jwtBuilder;
    private static AccessToken? accessToken;
    private readonly long Installation = 11111111;
    private readonly string SnsTopicArn = ""; 
    private readonly string WebHookSecret = "";


    public Function()
    {
        if (jwtBuilder != null) return;

        SnsTopicArn = Environment.GetEnvironmentVariable("SNSTOPIC_ARN")!;
        Installation = long.Parse(Environment.GetEnvironmentVariable("INSTALLATION_ID")!);
        WebHookSecret = Environment.GetEnvironmentVariable("WEBHOOK_SECRET")!;
        var iss = Environment.GetEnvironmentVariable("GITHUB_APPID")!;  //Issuer is your APP ID
        var privKey = Convert.FromBase64String(Environment.GetEnvironmentVariable("RSA_PRIVATEKEY")!);

        jwtBuilder = new JwtBuilder(privKey, iss);
    }

    /// <summary>
    /// Function that accepts a WebHook request (wrapped in API Proxy request format) from GitHub, and
    /// handles it by protecting default branch of newly-created repo, creating issue in the repo, and sending a notification.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
    {
        // Validate the request secret (passed in HTTP header)
        if (!IsRequestSignatureValid(input.Body, input.Headers))
            return new APIGatewayProxyResponse { StatusCode = 403, Body = "{ 'Error': 'Forbidden: Invalid secret'}" };

        var bodyNode = JsonNode.Parse(input.Body);

        if (bodyNode is null)
            return new APIGatewayProxyResponse { StatusCode = 400, Body = "{ 'Error': 'missing body' }" };

        // Only handle newly-created repos
        var action = bodyNode["action"]?.GetValue<string>() ?? "";
        var repoNode = bodyNode["repository"];

        if (action != "created" || repoNode is null)
            return new APIGatewayProxyResponse { StatusCode = 200, Body = "Ignored" };

        var repoName = repoNode["full_name"]!.GetValue<string>();
        var repoId = repoNode["id"]!.GetValue<long>();
        var defaultBranchName = repoNode["default_branch"]!.GetValue<string>();
        Console.WriteLine($"Received repo-created webhook for repo '{repoName}' with id {repoId}");

        // Publish message to SNS with contents of repoNode
        SendNotification(repoNode.ToJsonString());

        // fetch GitHub installation access token if none exists, or expired
        RefreshInstallationAccessToken();

        // Set up GitHub client with token auth
        var tokenAuth = new Credentials(accessToken!.Token);
        var ghc = new GitHubClient(new ProductHeaderValue("branch-protection-lambda"));
        ghc.Credentials = tokenAuth;

        // If the default branch doesn't exist, create it.
        EnsureDefaultBranchExists(ghc, repoId, defaultBranchName);

        // Protect the default branch
        ProtectDefaultBranch(ghc, repoId, defaultBranchName);

        // Create issue in new repo explaining that the branch is protected
        CreateIssue(ghc, repoId);

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = repoName
        };
    }

    private bool IsRequestSignatureValid(string body, IDictionary<string, string> headers)
    {
        if (headers == null) return false;

        if (!headers.Any(x => x.Key.Equals("X-Hub-Signature-256", StringComparison.InvariantCultureIgnoreCase)))
        {
            Console.WriteLine($"Missing header X-Hub-Signature-256");
            return false;
        }
            
        var headerSig = headers.First(x => x.Key.Equals("X-Hub-Signature-256", StringComparison.InvariantCultureIgnoreCase)).Value;
        
        using var sha = new HMACSHA256(Encoding.UTF8.GetBytes(WebHookSecret));
        var hash = "sha256=" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", "");

        return hash.Equals(headerSig, StringComparison.InvariantCultureIgnoreCase);
    }

    private void RefreshInstallationAccessToken()
    {
        if (accessToken != null && accessToken.ExpiresAt.AddMinutes(1) > DateTime.UtcNow)
            return;

        // create JWT to use with bearer auth to fetch GitHub App installation token
        var jwtstring = jwtBuilder!.GetToken();

        var jwtAuth = new Credentials(jwtstring, AuthenticationType.Bearer);
        var conn = new Connection(new ProductHeaderValue("branch-protection-lambda")) { Credentials = jwtAuth };
        var ghAppClient = new GitHubAppsClient(new ApiConnection(conn));

        accessToken = ghAppClient.CreateInstallationToken(Installation).Result;
    }

    private void EnsureDefaultBranchExists(GitHubClient ghc, long repoId, string defaultBranchName)
    {
        var allBranches = ghc.Repository.Branch.GetAll(repoId).Result;
        var defaultBranch = allBranches.FirstOrDefault(x => x.Name.Equals(defaultBranchName));

        if (defaultBranch == null)
        {
            var createRequest = new CreateFileRequest(
                "Automatically creating ReadMe file to create default branch in empty repo",
                "## Blank README file",
                defaultBranchName);
            var createFileResponse = ghc.Repository.Content.CreateFile(repoId, "README.md", createRequest).Result;
        }
    }

    private void CreateIssue(GitHubClient ghc, long repoId)
    {
        string issuebody = GetIssueBody();

        try
        {
            var newIssueRequest = new NewIssue("Branch protections enabled for this repo");
            newIssueRequest.Body = issuebody;
            newIssueRequest.Assignees.Add("Kirkaiya");
            var issue = ghc.Issue.Create(repoId, newIssueRequest).Result;

            Console.WriteLine($"Created new issue at {issue.HtmlUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating issue: {ex.Message}");
        }
    }

    private void ProtectDefaultBranch(GitHubClient ghc, long repoId, string defaultBranchName)
    {
        var dismissal = new BranchProtectionRequiredReviewsDismissalRestrictionsUpdate(false);
        var update = new BranchProtectionSettingsUpdate(null, new BranchProtectionRequiredReviewsUpdate(dismissal, false, false, 1), true);
        ghc.Repository.Branch.UpdateBranchProtection(repoId, defaultBranchName, update).Wait();
    }

    private void SendNotification(string messageBody)
    {
        if (bool.Parse(Environment.GetEnvironmentVariable("SEND_SNS")!))
        {
            if (client is null)
                client = new AmazonSimpleNotificationServiceClient();

            var request = new PublishRequest
            {
                TargetArn = SnsTopicArn,
                Message = messageBody,
                Subject = "GitHub Webhook Handler message"
            };

            client.PublishAsync(request).Wait();
            Console.WriteLine("Published SNS message to topic.");
        }
    }

    private string GetIssueBody()
    {
        using Stream stream = GetType().Assembly.GetManifestResourceStream("orgbranchprotection.issuetext.md")!;
        using StreamReader sr = new(stream);
        return sr.ReadToEnd();
    }
}
