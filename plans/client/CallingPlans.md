# Edemly Calls Roadmap

## Phase 1. Stabilize 1-on-1 Audio Calls

### Goal

Ensure existing audio calls work reliably.

### Tasks

* Fix call window opening/closing behavior.
* Properly terminate calls for both participants.
* Handle call states:

  * Incoming
  * Accepted
  * Rejected
  * Ended
  * Missed
* Test repeated calls after a previous call has ended.
* Handle participant disconnects.
* Handle network connection loss.

### Result

Stable 1-on-1 audio calling.

---

## Phase 2. Call Security

### Goal

Verify and improve call security.

### Tasks

* Verify WebRTC encryption.
* Verify DTLS/SRTP usage.
* Ensure media streams are not routed through the application server.
* Protect SignalR signaling with JWT authorization.
* Restrict call access to chat participants only.
* Validate `chat_id`, `call_id`, and `user_id` during signaling.

### Result

Secure and authenticated call infrastructure.

---

## Phase 3. Call History

### Goal

Store and display call information.

### Database

#### Call

```text
Call
----
Id
ChatId
InitiatorId
StartedAt
EndedAt
Status
```

#### CallParticipant

```text
CallParticipant
---------------
Id
CallId
UserId
JoinedAt
LeftAt
```

### Tasks

* Save call start/end information.
* Save initiator information.
* Save participants.
* Track call status:

  * Completed
  * Missed
  * Rejected
  * Failed
* Display call history in chats.

### Result

Users can view previous calls and participation history.

---

## Phase 4. Group Audio Calls

### Goal

Support calls with multiple participants.

### Tasks

* Implement participant join/leave logic.
* Display active participant list.
* Show speaking indicators.
* Allow participants to leave without ending the call.
* Automatically end calls when no active participants remain.

### Result

Fully functional group audio calls.

---

## Phase 5. 1-on-1 Video Calls

### Goal

Add video support to existing WebRTC infrastructure.

### Tasks

* Add camera stream support.
* Add camera enable/disable controls.
* Implement video call UI.
* Handle unavailable camera devices.
* Support switching cameras.

### Result

1-on-1 video calling.

---

## Phase 6. Call Recording (Premium Feature)

### Goal

Allow recording of calls.

### Access Rules

* Premium subscription.
* Company workspaces.
* Administrators.

### Database

#### CallRecording

```text
CallRecording
-------------
Id
CallId
FilePath
Duration
Size
CreatedAt
```

### Tasks

* Start/stop recording.
* Store recordings.
* Add playback functionality.
* Restrict access to call participants.
* Show recording metadata.

### Result

Recorded calls available for playback.

---

## Phase 7. Call Transcription

### Goal

Generate text transcripts from recordings.

### Database

#### CallTranscript

```text
CallTranscript
--------------
Id
RecordingId
Content
Language
CreatedAt
```

### Tasks

* Process recordings after call completion.
* Run transcription through Whisper or another speech-to-text service.
* Save transcript data.
* Display transcripts in the UI.
* Add transcript search.

### Result

Searchable text transcripts of conversations.

---

## Phase 8. Group Video Calls

### Goal

Support video conferencing.

### Tasks

* Display multiple remote video streams.
* Create dynamic grid layout.
* Optimize video stream management.
* Support camera/microphone controls.
* Handle weak network conditions.

### Result

Group video conferencing functionality.

---

## Phase 9. Screen Sharing

### Goal

Allow participants to share their screens.

### Tasks

* Capture screen stream.
* Switch between camera and screen streams.
* Display sharing indicators.
* Allow stopping screen sharing.
* Handle permission denials.

### Result

Screen sharing during calls.

---

## Phase 10. Call Management & Permissions

### Goal

Provide moderation and administration capabilities.

### Roles

* Owner
* Moderator
* Participant

### Features

* Invite by link.
* Join by link.
* Kick participant.
* Mute participant.
* Lock call.
* Approve/deny join requests.
* Moderator action logs.

### Result

Enterprise-level call management.

---

## Phase 11. Call Links and Deep Join

### Goal

Allow users to join calls through secure app links.

### Tasks

* Generate call invite links.
* Open call links in the desktop client.
* Include tenant/company context in call links.
* Expire call links when the call ends.
* Add waiting room or approval flow for invited users.
* Prevent call links from bypassing chat or call permissions.

### Result

Users can join calls by link without weakening access control.

---

## Phase 12. Advanced Call Controls

### Goal

Improve group call interaction quality.

### Features

* Noise suppression.
* Raise hand.
* Better call sounds.
* Speaking indicators.
* Moderator mute controls.

### Tasks

* Research client-side noise suppression options.
* Add raise-hand state and SignalR events.
* Display raised hands in participant list.
* Add moderator actions for muting participants.
* Replace current call sounds with polished assets.

### Result

Group calls become more comfortable and manageable.

---

## Phase 13. AI Call Notes

### Goal

Turn call transcripts into useful meeting notes.

### Tasks

* Use call transcription as the input.
* Generate summary notes after the call.
* Extract action items and possible assignees.
* Save generated notes as editable drafts.
* Add permission checks for transcript and AI note access.

### Result

Calls can produce follow-up notes and tasks.

---

# Recommended Implementation Order

1. Stabilize 1-on-1 audio calls.
2. Call history.
3. Security review.
4. Group audio calls.
5. Call links and permission-safe join flow.
6. 1-on-1 video calls.
7. Advanced call controls.
8. Call recording.
9. Call transcription.
10. AI call notes.
11. Group video calls.
12. Screen sharing.
13. Permissions and moderation.
