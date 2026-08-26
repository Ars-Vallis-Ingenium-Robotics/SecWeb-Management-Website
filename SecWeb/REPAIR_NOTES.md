# SecWeb Repair Notes

This repaired copy keeps the existing SecWeb features while fixing the routing/render-mode conflict that caused Identity pages such as **Login** and **Register** to show **Not Found**.

## Main repairs

- `Components/App.razor` now switches between:
  - `InteractiveServer` for the main SecWeb application.
  - Static server-side rendering for Identity pages marked with `ExcludeFromInteractiveRouting`.
- `Members.razor` and `Admin/UserManagement.razor` were moved out of the static Identity page folder so their interactive buttons work.
- The cookie-consent component is now included in `MainLayout.razor`.
- The account-management UI no longer exposes phone-number, passkey, authenticator-app, recovery-code, or optional 2FA pages.
- Mandatory email login verification remains enabled.
- Global roles are now only `Member` and `Admin`.
  - The old global `Subsystem Lead` role is migrated to `Member` at startup.
  - Project/team leadership remains a project-level `Lead` assignment.
- `secretary@avirobotics.org` remains the protected permanent Admin account.
- User Management now creates and manages only Member/Admin accounts.
- Form-binding warnings in the custom Identity pages were corrected.
- Build output folders (`bin` and `obj`) were removed from this archive so Visual Studio generates clean output.

## Before running

1. Open `SecWeb.csproj` in Visual Studio 2026.
2. Confirm your existing Visual Studio User Secrets are still available. The project keeps the original `UserSecretsId`.
3. If needed, restore these secrets:
   - Email SMTP username, app password, and sender address.
   - Chipy bot token, Discord guild/channel IDs, OAuth client ID/secret, and redirect URI.
4. In Package Manager Console, run:

```powershell
Update-Database
```

5. Use **Build > Clean Solution**, then **Build > Rebuild Solution**.
6. Start the HTTPS profile.

## Quick verification

- Logged out:
  - Dashboard opens.
  - Register opens.
  - Login opens instead of showing Not Found.
- Logged in:
  - Meetings, Projects, Member List, and Time Tracking buttons respond.
  - My Account pages load with static Identity rendering.
- Admin:
  - User Management appears and opens.
  - Member/Admin account creation works.
- Cookie banner:
  - Appears until accepted, then remains hidden for that browser.

## Secrets

No bot token, Discord client secret, Gmail app password, or other User Secrets are included in this archive.
