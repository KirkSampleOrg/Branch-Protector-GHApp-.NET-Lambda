# Branch Protector GitHub App for AWS Lambda in .NET 6

A solution for automating the protection of the default branch in newly-created GitHub repos. The solution is built on .NET 6 and configured to run in an AWS Lambda function using the Lambda Function URL feature to expose it over HTTP.

## Background
Over time, organizations often find they create an increasing number of code repos in GitHub. Manual processes to enforce quality controls, such as ensuring code is reviewed prior to being merged into the main branch, can fail to keep up. Fortunately, GitHub apps can be used to automate this process. This solution implements a GitHub app that automatically applies branch protection rules to the default branch in newly-created repos. If the new repo was created without a default branch, the solution will create the default branch (using the branchname configured as default for the GitHub organization) in the repo by creating a README file in the root of the repo.

**The solution consists of:**

 + [GitHub App](https://docs.github.com/en/developers/apps/getting-started-with-apps/about-apps).  GitHub Apps are the officially recommended way to integrate with GitHub.
 + [AWS Lambda function](https://aws.amazon.com/lambda/) written in C# for .NET 6
   + The project uses the [Octokit GitHub API Client Library for .NET](https://github.com/octokit/octokit.net)
   + [The Lambda Function URL](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html) feature is used to avoid the need to set up an Amazon API Gateway. Lambda Function URLs were introduced in April 2022

The following sections show a high-level diagram of the solution, and then details on how to implement each of the above items. **This is a proof of concept - do not put this solution into production unless/until you review and understand all aspects of what it's doing**.

## Solution Overview

This solution uses a GitHub App that is subscribed to repository events - including the creation of new repos - and which is configured with a webhook URL pointing at an AWS Lambda function that is configured with a function URL. The GitHub App is installed for an account (such as an organization). When users in the account create new repositories, a webhook event fires, which sends an HTTP POST message to the Lambda function.

The Lambda function uses a private key that you generate when configuring the GitHub App's authentication to generate a digitally-signed JWT (JSON Web Token). The Lambda function uses this JWT to a retrieve short-lived installation-access token from the GitHub API, which it uses in subsequent requests. The Lambda function uses the [octokit.net NuGet package](https://www.nuget.org/packages/Octokit) to make calls to the GitHub API at api.github.com.

The Lambda function then checks whether a default branch exists in the newly-created repo (using the repo's configured default branch name that is sent in the webhook payload). If the branch doesn't exist, the Lambda function creates it by creating a blank readme file for that branch. Then, the Lambda function applies branch protection settings to the branch, and finally creates a new issue in the Repo that mentions you (or the user you configure). Optionally, the Lambda function can push a notification to an [AWS SNS topic](https://aws.amazon.com/sns/) if you desire - this could be useful for notifying a security team of newly-created repositories, for example.

See the diagram below for a high-level illustration of the flow.

![Solution Diagram](docs/Solution-diagram.png)

## How to implement the solution

_Note: you will deploy the .NET 6 project to AWS Lambda before creating the Github App because the Lambda function's URL is needed when configuring the GitHub App._

### Deploy the .NET 6 application to AWS Lambda

Ensure you can build the `orgbranchprotection.csproj` project locally - it targets .NET 6.0. Then edit the text of the issues that get automatically created by the code, and deploy the code to a new Lambda function. Finally, you'll create a URL for your new Lambda function.

**Issue Text**

Open the file `/src/issuetext.md` in a text editor.  Replace the mention of me (@Kirkaiya) with the GitHub handle of whatever user you want mentioned in the issues that the Lambda function creates, and feel free to edit the issue markdown text as desired.

**Deploy as a new .NET Lambda function**

The easist way to deploy the code is using Visual Studio (2019 or 2022) with the [AWS Toolkit for Visual Studio](https://aws.amazon.com/visualstudio/) extension. You can also deploy the Lambda function using the [.NET Core Global Tools for AWS](https://aws.amazon.com/blogs/developer/net-core-global-tools-for-aws/), which let you deploy .NET Lambda functions using the command line, which is especially useful for automating deployments in CI/CD pipelines.

_Note: if you plan to have the Lambda function publish notifications to an SNS topic, you'll need to modify the [Lambda execution role](https://docs.aws.amazon.com/lambda/latest/dg/lambda-permissions.html) to give it [permissions](https://docs.aws.amazon.com/lambda/latest/dg/lambda-permissions.html) to publish to your SNS topic._

**Lambda Function URL**

After you deploy the _orgbranchprotection.csproj_ project to a new AWS Lambda function, in the AWS Lambda console, select the 'Configuration' tab, then select 'Function URL' from the left menu. Select 'Create function URL', then under 'Auth type' select 'None'.
_Note: the C# code in the project validates the GitHub App's webhook secret, so the authentication is happening in code_.

Save your changes, and make note of the function's URL. You'll need it below.

### Create and register a new GitHub App

Follow the steps in the GitHub docs for [Creating a GitHub App](https://docs.github.com/en/developers/apps/building-github-apps/creating-a-github-app). As you get to each of the steps below, follow the instructions for that step:

**Step 7**. For your app's website URL, you could use the URL of the repo containing your fork/copy of this code, or an intranet site with documentaion for your implementation of this solution.

**Step 8**. Leave the callback URL blank.

**Steps 9 - 12**. Skip steps 9 through 12

**Step 13.** Paste your Lambda function URL from the previous section into the Webhook URL field.

**Step 14**. Enter in a secret for your webhook. Follow the guidance in the GitHub docs for [Securing your webhooks](https://docs.github.com/en/developers/webhooks-and-events/webhooks/securing-your-webhooks) to choose a secret.  **Store your secret somewhere secure and never share it - you will need it to configure your AWS Lambda**.

**Step 15**. For repository permissions, select `read and write` for 'Administration', 'Contents' and 'Issues'. Select `read-only` for 'Metadata'. 

**Step 16**. Under 'Subscribe to events', select `Repository`.

### Configure authentication for your GitHub App

Follow the instructions in [Authenticating with GitHub Apps](https://docs.github.com/en/developers/apps/building-github-apps/authenticating-with-github-apps) to generate a private key. When you generate a new key, it will automatically download in your browser as a .pem file. Store your .pem file in a secure place, and never share it. You'll need it to configure your Lambda function.

### Install your GitHub App in your organization (or account)

Follow the instructions in the GitHub docs for [Installing GitHub Apps](https://docs.github.com/en/developers/apps/managing-github-apps/installing-github-apps) to install your app for your organization or GitHub account. Be sure to select "All repositories", so that your app can interact with newly-created repos.

### Configure Lambda function environment variables

Now that your GitHub app is configured and installed for your organization (or account), you'll need to configure your Lambda function with environment variables that correspond to your GitHub App's App ID, the installation ID, and the private key you generated. You can also specify an AWS SNS (Simple Notification Service) topic ARN if you want to have the Lambda function send notifications when it applies protections to a newly-created repo. Lambda function environment variables are encrypted at rest for security.

In your AWS Lambda console, in the 'Configuration' tab for your function, select 'Environment Variables' in the left menu, and then select 'Edit'. Add a new Environment Variable (a Key and Value) for each of the items below:

**App ID**

If you didn't make a note of your App ID earlier, go to your organization/account's settings, expand 'Developer Settings' on the left menu, and select 'GitHub Apps'. Then, click the 'Edit' button for this app. The App ID for the application is then displayed under the 'About' section (it's a number).  Create a new environment variable with Key = `GITHUB_APPID` and and enter the App ID into the value field.

**Installation ID**

Your GitHub App could be installed in multiple organizations and accounts, and we need the installation ID for the installation you did earlier. From your organization/account's settings, under 'Integrations' in the left menu, select 'GitHub Apps' (_Note: not under Developer Settings_). This will show the list of installed apps (see screenshot below). Select the 'Configure' button to show the app's permissions; the installation ID is the number at the end of the URL. For example, if the URL in your browser is `https://github.com/organizations/KirkSampleOrg/settings/installations/12345678`, the installation ID is 12345678.

![Installed GitHub Apps list](docs/installed-github-apps.png)
Create a new environment variable with Key = `INSTALLATION_ID` and enter the installation ID into the value field.

**Private Key**

Rather than storing the private key (the .pem file you generated earlier) in your repo and loading it from a file, which would risk exposing it, we'll store the contents in an environment variable. An even better solution would be storing the key contents in an AWS Secrets Manager secret, but for the purposes of this solution that's out of scope. We'll need to get the key text all in a single line of text for this.

Open up the .pem file in a text editor, and carefully delete the first line ("-----BEGIN RSA PRIVATE KEY-----") and the last line ("-----END RSA PRIVATE KEY-----"), then carefully delete all the line-breaks until there is a single row of text with no line breaks (be careful not to delete any of the characters of the key).

Create a new environment variable with the Key = `RSA_PRIVATEKEY` and enter the entire single line of key text into the value field.

**SNS Flag and ARN**

The .NET code as written checks to see if SNS notifications are enabled, and if so, sends the JSON contents of the webhook payload's repository node as a notification to an SNS topic. If you don't remove the code that handles this, you'll need to create two environment variables to avoid errors.

Create a new environment variable with the Key = `SEND_SNS` and enter either `True` or `False` in the value field. If you enter True, you'll need to [create an AWS SNS topic](tps://docs.aws.amazon.com/sns/latest/dg/sns-create-topic.html) in your AWS account and make note of the topic's ARN.

Create another new environment variable with the Key = `SNSTOPIC_ARN`. If you entered False for the SEND_SNS variable, you can leave the value blank. Otherwise, enter the SNS topic ARN as the value.

**You've now completed setting up the orgbranchprotectionauto GitHub app in your organization or account**.  Test the solution by creating a new repository, and then checking to see if branch protection settings were applied, and a new issue created.

