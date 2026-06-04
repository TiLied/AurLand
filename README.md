# AurLand
Personal helper script for aur, automate downloading, installing, and updating aur packages. To download visit [releases](https://github.com/TiLied/AurLand/releases).

Uploading to github just to not lose the source code :)

## Requires:
- pacman (-Qqm, -Rs)
- makepkg (-si)
- git (clone, pull, diff, rev-parse)

## aurland --help:
```bash
Description:
  AurLand is an aur helper.

Usage:
  aurland [command] [options]

Options:
  -dp, --data-prefix <data-prefix>    Path for an AurLand data prefix. By default (empty string), AurLand uses XDG_DATA_HOME.
  -s, --sync                          Sync already installed aur packages with AurLand. 
                                                Same as the sync command. 
                                                Runs first if multiple options are specified.
  -u, --update                        Update already installed aur packages with AurLand. 
                                                Same as the update command.
                                                Runs second if multiple options are specified.
  -i, --install <package> (REQUIRED)  Install aur package with AurLand. 
                                                Same as the install command.
                                                Runs third if multiple options are specified.
  -r, --remove <package> (REQUIRED)   Remove aur package with AurLand.
                                                Same as the remove command.
                                                Runs fourth if multiple options are specified.
                                                Needs to be run with sudo.
  -?, -h, --help                      Show help and usage information
  --version                           Show version information

Commands:
  sync               Sync already installed aur packages with AurLand.
  update             Update aur packages.
  install <package>  Install aur package.
  remove <package>   Remove aur package. Needs to be run with sudo.
  clear              Clear AurLand git folder.

```
