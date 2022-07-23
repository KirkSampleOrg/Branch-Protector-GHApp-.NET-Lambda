# Branch Protector GitHub App for AWS Lambda in .NET 6

A solution for automating the protection of the default branch in newly-created GitHub repos. The solution is built in .NET 6 and configured to run in an AWS Lambda function using the Lambda Function URL feature to expose it over HTTP.

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

The Lambda function uses a private key that you generate when configuring the GitHub App's authentication to generate a digitally-signed JWT (JASON Web Token). The Lambda function uses this JWT to a retrieve short-lived installation-access token from the GitHub API, which it uses in subsequent requests. The Lambda function uses the [octokit.net NuGet package](https://www.nuget.org/packages/Octokit) to make calls to the GitHub API at api.github.com.

The Lambda function then checks whether a default branch exists in the newly-created repo (using the repo's configured default branch name that is sent in the webhook payload). If the branch doesn't exist, the Lambda function creates it by creating a blank readme file for that branch. Then, the Lambda function applies branch protection settings to the branch, and finally creates a new issue in the Repo that mentions you (or the user you configure). See the diagram below for a high-level illustration of the flow.

![Solution Diagram](docs/Solution-diagram.png)

## How to implement the solution

_Note: you will deploy the .NET 6 project to AWS Lambda before creating the Github App because the Lambda function's URL is needed when configuring the GitHub App._

### Deploy the .NET 6 application to AWS Lambda

TBD

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


![Installed GitHub Apps list](docs/installed-github-apps.png)

