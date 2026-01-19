Every time I start a new .NET project, I find myself hunting through old projects to copy the `tasks.json` configuration. This post documents the setup I use so I can find it in one place.

## What tasks.json Does

VS Code's `tasks.json` file (in the `.vscode` folder) defines custom tasks that run from the Command Palette (Ctrl+Shift+P → "Run Task") or via keyboard shortcuts. For .NET projects, this typically means:

- Building the solution
- Running the project
- Running tests
- Publishing/deploying
- Building CSS (for projects using Tailwind or similar)

## The Full Configuration

Here's my current `tasks.json` for an ASP.NET MVC project with Tailwind CSS and Web Deploy publishing:

```json
{
    "version": "2.0.0",
    "inputs": [
        {
            "id": "deployPassword",
            "type": "promptString",
            "description": "Enter deployment password",
            "password": true
        }
    ],
    "tasks": [
        {
            "label": "Build",
            "type": "process",
            "command": "dotnet",
            "args": [
                "build",
                "${workspaceFolder}/ProjectName.sln"
            ],
            "problemMatcher": "$msCompile",
            "group": {
                "kind": "build",
                "isDefault": true
            }
        },
        {
            "label": "Run",
            "type": "shell",
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "${workspaceFolder}/ProjectName/ProjectName.csproj"
            ],
            "dependsOn": ["Build CSS"],
            "problemMatcher": "$msCompile",
            "presentation": {
                "reveal": "always",
                "panel": "dedicated"
            }
        },
        {
            "label": "Test",
            "type": "process",
            "command": "dotnet",
            "args": [
                "test",
                "${workspaceFolder}/ProjectName.sln"
            ],
            "problemMatcher": "$msCompile",
            "group": "test"
        },
        {
            "label": "Update Browserslist",
            "type": "shell",
            "command": "npx",
            "args": ["update-browserslist-db@latest"],
            "options": {
                "cwd": "${workspaceFolder}/ProjectName"
            },
            "problemMatcher": []
        },
        {
            "label": "Build CSS",
            "type": "shell",
            "command": "npm",
            "args": ["run", "build:css"],
            "options": {
                "cwd": "${workspaceFolder}/ProjectName"
            },
            "dependsOn": ["Update Browserslist"],
            "problemMatcher": []
        },
        {
            "label": "Watch CSS",
            "type": "shell",
            "command": "npm",
            "args": ["run", "watch:css"],
            "options": {
                "cwd": "${workspaceFolder}/ProjectName"
            },
            "isBackground": true,
            "problemMatcher": [],
            "presentation": {
                "reveal": "always",
                "panel": "dedicated"
            }
        },
        {
            "label": "Publish",
            "type": "shell",
            "command": "dotnet",
            "args": [
                "publish",
                "${workspaceFolder}/ProjectName/ProjectName.csproj",
                "-c",
                "Release",
                "-p:PublishProfile=ProfileName",
                "-p:Password=${input:deployPassword}"
            ],
            "dependsOn": ["Build CSS"],
            "problemMatcher": "$msCompile",
            "presentation": {
                "reveal": "always",
                "panel": "shared"
            },
            "group": "build"
        }
    ]
}
```

## Key Concepts

### Inputs for Secrets

The `inputs` array defines values prompted at runtime:

```json
"inputs": [
    {
        "id": "deployPassword",
        "type": "promptString",
        "description": "Enter deployment password",
        "password": true
    }
]
```

Reference it in tasks with `${input:deployPassword}`. The `password: true` flag masks input.

This keeps credentials out of the file while still enabling one-command deployment.

### Task Dependencies

The `dependsOn` property chains tasks:

```json
{
    "label": "Run",
    "dependsOn": ["Build CSS"],
    ...
}
```

The "Run" task automatically runs "Build CSS" first. This ensures Tailwind CSS is compiled before the app starts.

### Keeping Browserslist Updated

If you use Tailwind CSS with PostCSS, you'll eventually see this warning during builds:

```
Browserslist: caniuse-lite is outdated. Please run:
  npx update-browserslist-db@latest
```

Rather than running this manually, add a task that runs automatically before CSS builds:

```json
{
    "label": "Update Browserslist",
    "type": "shell",
    "command": "npx",
    "args": ["update-browserslist-db@latest"],
    "options": {
        "cwd": "${workspaceFolder}/ProjectName"
    },
    "problemMatcher": []
}
```

Then chain it to your CSS build task:

```json
{
    "label": "Build CSS",
    "dependsOn": ["Update Browserslist"],
    ...
}
```

Now every CSS build ensures the browserslist database is current. The update is fast when already current, so it doesn't add noticeable overhead.

### Problem Matchers

Problem matchers parse task output and populate VS Code's Problems panel:

```json
"problemMatcher": "$msCompile"
```

The `$msCompile` matcher understands .NET compiler output, so build errors become clickable links to the source.

For tasks that don't produce parseable errors (like npm scripts), use an empty array:

```json
"problemMatcher": []
```

### Background Tasks

For long-running watchers:

```json
{
    "label": "Watch CSS",
    "isBackground": true,
    "presentation": {
        "panel": "dedicated"
    }
}
```

The `isBackground: true` flag tells VS Code this task doesn't terminate, and `panel: dedicated` gives it its own terminal.

### Task Groups

Groups organize tasks in the Command Palette:

```json
"group": {
    "kind": "build",
    "isDefault": true
}
```

The default build task runs with Ctrl+Shift+B.

### Working Directory

For tasks that need to run in a subdirectory:

```json
"options": {
    "cwd": "${workspaceFolder}/ProjectName"
}
```

This is necessary when `package.json` lives in the project folder rather than the solution root.

## Publishing with Web Deploy

The publish task uses a publish profile (`.pubxml` file in `Properties/PublishProfiles/`):

```json
{
    "label": "Publish",
    "args": [
        "publish",
        "${workspaceFolder}/ProjectName/ProjectName.csproj",
        "-c",
        "Release",
        "-p:PublishProfile=ProfileName",
        "-p:Password=${input:deployPassword}"
    ],
    "dependsOn": ["Build CSS"]
}
```

The password is passed via `-p:Password=` because Web Deploy publish profiles don't store passwords.

### Publish Profile Setup

The `.pubxml` file in `Properties/PublishProfiles/` contains:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <WebPublishMethod>MSDeploy</WebPublishMethod>
    <PublishProvider>AzureWebSite</PublishProvider>
    <LastUsedBuildConfiguration>Release</LastUsedBuildConfiguration>
    <LastUsedPlatform>Any CPU</LastUsedPlatform>
    <SiteUrlToLaunchAfterPublish>https://yoursite.com/</SiteUrlToLaunchAfterPublish>
    <LaunchSiteAfterPublish>false</LaunchSiteAfterPublish>
    <MSDeployServiceURL>yourhost.com</MSDeployServiceURL>
    <DeployIisAppPath>yoursite.com</DeployIisAppPath>
    <RemoteSitePhysicalPath />
    <SkipExtraFilesOnServer>false</SkipExtraFilesOnServer>
    <MSDeployPublishMethod>WMSVC</MSDeployPublishMethod>
    <EnableMSDeployBackup>true</EnableMSDeployBackup>
    <UserName>deployuser</UserName>
  </PropertyGroup>
</Project>
```

## Running Tasks

- **Command Palette**: Ctrl+Shift+P → "Tasks: Run Task" → select task
- **Default Build**: Ctrl+Shift+B runs the default build task
- **Terminal Menu**: Terminal → Run Task

## Keyboard Shortcuts

For frequently-used tasks, add keybindings in `keybindings.json`:

```json
{
    "key": "ctrl+shift+r",
    "command": "workbench.action.tasks.runTask",
    "args": "Run"
}
```

## Adapting for Your Project

To use this configuration:

1. Copy `.vscode/tasks.json` to your project
2. Replace `ProjectName` with your actual project/solution names
3. Replace `ProfileName` with your publish profile name
4. Adjust paths if your structure differs
5. Remove CSS tasks if not using Tailwind/npm

The file is version-controlled, so team members get the same task definitions.
