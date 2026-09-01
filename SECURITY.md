# Security policy

## Supported versions

Until the first stable release, only the latest commit on `main` is supported. After stable releases begin, this file will identify the maintained release lines.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could cause code execution, unsafe window automation, path handling problems, or release-signing compromise.

Use GitHub's **Report a vulnerability** private security-advisory feature for this repository. Include:

- affected commit or version;
- Windows and Okular versions;
- reproduction steps;
- expected and observed behavior;
- relevant logs with personal file paths removed;
- impact and any proposed mitigation.

The maintainers will acknowledge a complete report as soon as practical, investigate it privately, and coordinate disclosure after a fix is available. Please do not include confidential documents or signing credentials.

## Security boundaries

OkularTabLauncher:

- accepts one local PDF path;
- automates an existing desktop application in the current interactive session;
- does not parse or render PDF data;
- does not require administrator privileges;
- does not download or execute updates;
- does not bypass Smart App Control or other Windows security controls.

OkularSessionLauncher:

- reads tab names and the active window title through Windows UI Automation;
- stores local PDF paths under the current user's `%LOCALAPPDATA%` profile;
- never stores PDF contents in its session file;
- can close and reopen a newly detected Okular window when restoring a session;
- refuses that automatic restart when the new window already contains multiple tabs;
- does not require administrator privileges or bypass Windows security controls.

A signed release establishes publisher and artifact integrity; it does not guarantee that a PDF is safe.
