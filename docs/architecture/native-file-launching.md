# Native file launching boundary

`CanDoItAll.FileTools.Desktop` owns the operating-system process boundary for opening an existing local file or folder. FileBrowser and FileInteraction remain provider-neutral Blazor packages and only raise typed host requests.

The host is responsible for authorization and for resolving an opaque provider item to a trusted absolute local path before creating a `DesktopFileLaunchRequest`. The desktop package validates that the target still exists at execution time.

System-default opening uses the operating system shell association. A configured executable is an explicit override: it is started directly with the target as one `ArgumentList` entry. If that executable is missing or cannot start, the request fails and does not fall back to the system association.

`OpenContainingFolder` resolves a file to its exact parent directory and leaves a directory unchanged. Selecting a file inside a platform file manager is deliberately outside this portable contract because operating systems do not expose a consistent selection mechanism.
