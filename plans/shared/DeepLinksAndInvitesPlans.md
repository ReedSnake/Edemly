# Edemly Deep Links and Invites Roadmap

## Phase 1. Define App Link Strategy

### Goal

Open profiles, group invites, and call invites in the desktop client even without a web version.

### Link Types

```text
edemly://profile/{userId}
edemly://group/{inviteCode}
edemly://call/{inviteCode}
edemly://theme/{themeId}
https://edemly.app/open/...
```

### Tasks

* Register a custom protocol handler for the desktop client.
* Decide whether public HTTPS links redirect into `edemly://` links.
* Add fallback pages for users who do not have the app installed later.
* Ensure tenant/company context is included in links.
* Validate all links server-side before opening sensitive data.

### Result

Links can route users into the correct place in the desktop app.

---

## Phase 2. Group Invite Links

### Goal

Allow users to join groups by invite link.

### Database

```text
GroupInvite
-----------
Id
TenantId
ChatId
Code
CreatedByUserId
ExpiresAt
MaxUses
UseCount
RevokedAt
CreatedAt
```

### Tasks

* Add invite creation for group admins/creators.
* Support expiring invites.
* Support single-use and limited-use invites.
* Allow admins to revoke invites.
* Add invite preview before joining.
* Require login before accepting private group invites.
* Add audit log entries for invite creation, use, and revoke.

### Result

Group joining works through controlled, revocable links.

---

## Phase 3. Call Invite Links

### Goal

Allow users to join calls by link.

### Tasks

* Create call invite tokens scoped to a call or chat.
* Decide if invitees must already be chat members.
* Add waiting room or approval flow for external invitees.
* Validate call status before allowing join.
* Expire call links after the call ends.
* Add moderator controls for removing invited participants.

### Result

Calls can be shared by link without bypassing call security.

---

## Phase 4. Profile Links

### Goal

Allow user profiles to be opened by link.

### Tasks

* Add profile deep link handling in the client.
* Decide what profile data is public, company-visible, or contact-only.
* Open profile by username or stable user id.
* Support tenant/company profile context.
* Add privacy settings later if needed.

### Result

Users can share profile links that open directly in Edemly.

---

## Phase 5. Theme Links

### Goal

Allow users to install shared themes by link.

### Tasks

* Define theme package format.
* Validate theme color values and assets.
* Preview theme before applying.
* Store installed user themes locally.
* Add import/export flow.

### Result

Themes can be shared safely between users.

