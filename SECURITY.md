# Security Policy

## Supported versions

No release has been published yet. Security fixes will be provided for the latest released version once the package ships.

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

Report vulnerabilities privately through GitHub: **Security → [Report a vulnerability](https://github.com/TagBites/TagBites.IO.AzureBlob/security/advisories/new)**.

Include a description, the affected version, and a minimal program that reproduces the issue. We aim to acknowledge reports within a few business days and to release a fix or mitigation as soon as a valid issue is confirmed.

## Security model

This package is a provider for [TagBites.IO](https://github.com/TagBites/TagBites.IO). The core security model - no sandbox, paths are the only limit, advisory permissions, content buffered through the system temporary directory - is described in the [core security policy](https://github.com/TagBites/TagBites.IO/blob/master/SECURITY.md). What follows is specific to this provider.

### Credentials

An Azure Storage connection string, which embeds the account key. That key grants full control of the storage account, not only the container in use. Prefer a scoped SAS or a managed identity where the deployment allows it.
