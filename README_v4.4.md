# v4.4 — Anonymous Staff and Unique Emails

- Regular users can open, edit permitted text, preview and save without signing in.
- Only Publisher and Administrator accounts may sign in. Existing legacy User accounts cannot sign in.
- Only Publisher/Administrator accounts can be created through account management.
- Publisher layout permission remains enforced; Administrators need a layout grant.
- Recovery email addresses must be unique (case-insensitive, ignoring leading/trailing spaces). The check runs before verification, before saving, and at the account store write boundary.
- Existing account data is retained. Existing duplicate emails must be resolved before further account writes.
- All other v4.3 features are retained.

Back up your project, open the .csproj in Visual Studio 2026, Build > Rebuild Solution. Windows WPF compilation was not available in this environment.
