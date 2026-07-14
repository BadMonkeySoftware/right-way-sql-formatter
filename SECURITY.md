# Security Policy

## Supported Versions

Only the latest released version of the VS Code extension (and the CLI it
bundles) is supported with security updates. There is no long-term support
for older versions — update to the newest release before reporting.

| Version        | Supported |
| -------------- | --------- |
| latest release | ✅        |
| anything older | ❌        |

## Reporting a Vulnerability

Please **do not open a public issue** for security problems.

Instead, use GitHub's private vulnerability reporting: go to the
[Security tab](https://github.com/BadMonkeySoftware/right-way-sql-formatter/security)
of this repository and choose **"Report a vulnerability"**.

What to expect:

- An acknowledgement within a week.
- A fix or a mitigation plan for confirmed issues in the next release, with
  credit in the changelog if you want it.
- If the report is declined (e.g. not reproducible or out of scope), an
  explanation of why.

Scope notes: the formatter parses untrusted SQL text, so parser crashes,
hangs, or memory exhaustion on crafted input are in scope. The CLI and
extension do not execute SQL, make network calls, or read files other than
those you format.
