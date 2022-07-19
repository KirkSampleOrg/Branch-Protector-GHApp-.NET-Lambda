# Branch Protector GitHub App for AWS Lambda in .NET 6

A solution for automating the protection of the default branch in newly-created GitHub repos. The solution is built in .NET 6 and configured to run in an AWS Lambda function using the Lambda Function URL feature to expose it over HTTP.

### This readme file will contain the documentation and instructions for deploying the solution

**Content TBD**

## Background
Over time, organizations often find they create an increasing number of code repos in GitHub. Manual processes to enforce quality controls, such as ensuring code is reviewed prior to being merged into the main branch, can fail to keep up. Fortunately, GitHub apps can be used to automate this process. This solution implements a GitHub app that automatically applies branch protection rules to the default branch in newly-created repos. If the new repo was created without a default branch, the solution will create the default branch (using the branchname configured as default for the GitHub organization) in the repo by creating a README file in the root of the repo.

**The solution consists of:**

 + [GitHub App](https://docs.github.com/en/developers/apps/getting-started-with-apps/about-apps).  GitHub Apps are the officially recommended way to integrate with GitHub.
 + [AWS Lambda function](https://aws.amazon.com/lambda/) written in C# for .NET 6
   + The project uses the [Octokit GitHub API Client Library for .NET](https://github.com/octokit/octokit.net)
   + [The Lambda Function URL](https://docs.aws.amazon.com/lambda/latest/dg/lambda-urls.html) feature is used to avoid the need to set up an Amazon API Gateway. Lambda Function URLs were introduced in April 2022

The following sections show a high-level diagram of the solution, and then details on how to implement each of the above items. **This is a proof of concept - do not put this solution into production unless/until you review and understand all aspects of what it's doing**.

_Note: you will deploy the .NET 6 project to AWS Lambda before creating the Github App because the Lambda function's URL is needed when configuring the GitHub App._

## High-level diagram of the solution

![Solution Diagram](docs/Solution-diagram.png)

## Deploy the .NET 6 application to AWS Lambda

TBD


## Create the GitHub App

TBD