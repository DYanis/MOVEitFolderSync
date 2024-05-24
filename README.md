# MOVEit Folder Sync
MOVEit Folder Sync is a tool designed to synchronize files between a local directory and MOVEit Cloud storage. The tool watches a specified local folder and automatically uploads any new files to the cloud, ensuring your files are always backed up and available.

### Features
* Automatic File Synchronization: Monitors a local directory and uploads files to MOVEit Cloud.
* Retry Logic: Implements retry logic for file uploads to handle temporary issues.
* Configurable Options: Allows customization of file synchronization settings through a configuration file.
* Automatic File Deletion: Deletes files from MOVEit Cloud when they are deleted from the local directory.
* Logging: Logs application events and errors to a file in the `logs` directory.

### Prerequisites
* .NET 8
* MOVEit Cloud API credentials `username` and `password`

## Getting Started
Clone the Repository:

```
git clone https://github.com/{your_github_username}/MOVEitFolderSync.git
```

### Configuration
Set up the configuration file:

1. Navigate to the `appsettings.json` file located in the `Application` directory of the project.
2. Update the `appsettings.json` file with the path of the local folder you want to monitor. Specifically, set the `LocalFolderPath` variable under `FileSyncWatcherOptions`:

![localpath](https://github.com/DYanis/MOVEitFolderSync/assets/10419828/c281e2d7-c52c-4993-8875-04bd435f9c08)

### Run the Console Application
Start the application and enter your MOVEit `username` and `password` when prompted:

```
dotnet run
```
![username_pass](https://github.com/DYanis/MOVEitFolderSync/assets/10419828/484f11ae-bffe-4247-b7db-9575723c0291)

### Logs
All logs are stored in the `logs` directory. These logs include information about file uploads, deletions, and any errors encountered during the synchronization process.

## Built With
* [Polly](https://github.com/App-vNext/Polly) - The resilience and transient-fault-handling library.
* [Serilog](https://serilog.net/) - Simple .NET logging with fully-structured events.

## Authors
* Dimitar Yanis - Initial work - [DYanis](https://github.com/DYanis)

## License
This project is licensed under the MIT License - see the [LICENSE](https://github.com/DYanis/MOVEitFolderSync/blob/master/LICENSE) file for details.
