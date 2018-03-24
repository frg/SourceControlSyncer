# SourceControlSyncer
A console application written in c# to sync all your repositories from Github, Bitbucket Server etc. to your machine.

**NOTE: Any merge conflicts will be resolved by using the remote's changes**

## Console arguments

### Generic

* `-?|--help` Shows the help
* `-p|--RepositoryPathTemplate` The path template the app will use to sync your individual repositories to
* `-rw|--RepositoryWhitelist` Provides a way to whitelist which repositories to sync
* `-bw|--BranchWhitelist` Provides a way to whitelist which branches to sync

### Bitbucket Server

* `-url|--ServerUrl` The Bitbucket Server that will be queried for repositories
* `-u|--Username` The username that will be used to authenticate with the Bitbucket Server
* `-e|--Email` The email that will be used with any source control signitures
* `-p|--Password` The password that will be used to authenticate you with Bitbucket Server

### Github

* `-at|--AccessToken` The access token that will be used to authenticate with Github (scopes: "repo", "read:org")
* `-u|--Username` The username that will be used to authenticate & interact with Github
* `-e|--Email` The email that will be used with any source control signitures

## TODO
* Implement merge handling
* Implement Bitbucket Cloud API
* Refactor code to remove redundancies

## Usage
`bitbucketserver -server "https://bitbucket.company.com" -username "tom.rodd" -password "bigwalrus35" -email "example@gmail.com" -branchwhitelist "develop,release"`

`github -username "rainbowrider" -accesstoken "23gyae12146f7335087g0d96819c7dfdc409rc22" -email "example@gmail.com"`
