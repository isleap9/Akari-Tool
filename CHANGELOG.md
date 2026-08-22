## v2.0.3

### New

- Akari Tool now offers to create a system restore point the first time you
  launch it, before any changes are made. Every tweak page also has a
  Create Restore Point quick action, and restore point creation itself was
  rebuilt on Windows' native API: it verifies the point actually exists,
  grows shadow storage automatically when it's nearly full, and no longer
  leaves your restore-point frequency setting permanently changed.
- Bulk actions show progress. Apply Recommended and Restore Defaults now run
  behind a progress card with a Cancel button, instead of silently churning
  through rows.
- The sidebar shows pending counts. Each tab carries a badge with how many
  of its recommended settings aren't applied yet, updating the moment you
  flip something.
- Every tweak row on the Gaming, Sound, Notifications, Privacy, Power and
  Update pages now shows an icon next to its name.
- Expandable technical details on every tweak row. See exactly what a tweak
  touches — registry paths and values, scheduled tasks, power settings,
  scripts — with Current, Recommended and Windows Default values side by
  side, and a button that opens regedit right at that key. Rows can also
  raise a status banner explaining what happened on the last apply.
- Recently added tweaks are flagged with a NEW badge.

### Smarter behavior

- Related tweaks now cascade the way they do in Windows. Disabling
  hibernation resets fast startup to its default, the Visual Effects mode
  preset drives its individual effect toggles, and cross-page requirements
  resolve automatically.
- Tweaks that don't apply to your machine are hidden instead of shown and
  broken: Windows 10-only rows disappear on Windows 11, build-bounded rows
  appear once you're on a new enough update, and Power rows drop out when
  the hardware doesn't support them.
- Visual Effects gained a mode dropdown — Let Windows Decide, Best
  Appearance, Best Performance, Custom — that sets every effect toggle at
  once.
- Gaming's Network section returned: Nagle's algorithm tuning per adapter
  plus the DNS selector with DNS-over-HTTPS (Cloudflare, Google, Quad9,
  OpenDNS).

### Fixes

- Dropdowns keep their selection across restarts again. The DNS Server,
  Windows Update Policy and Touch Keyboard Service selectors came back blank
  after relaunching even though the tweak stayed applied.
- Backup & Restore exports and imports every tweak again — including numeric
  values and the active power plan — and global search finds tweaks across
  all tabs once more. Both had gone quiet during the settings engine rebuild.
- ClearType font smoothing now actually applies; it was previously written in
  a form Windows silently ignores.
- The Power Plan dropdown keeps its selection when its contents refresh.
- Fixed garbled characters in some Customize page descriptions and search
  results.
- Corrected the UAC level labels.

### New tweaks

- Customize ▸ Explorer: remove the 3D Objects folder, hide duplicate
  removable drives, restore legacy Photo Viewer and Notepad file
  associations, single-click item opening, hide sync provider notifications.
- Customize ▸ Desktop: remove the desktop shortcut arrow.
- Customize ▸ Start Menu: clean all default pins.
- Gaming ▸ Security: PowerShell execution policy selector.
- The Context Menu subpage is gone — Classic Right-Click Menu now lives on
  the Explorer page and switches via the same mechanism Windows itself uses.

## v2.0.2

### Verify

- Akari Tool now checks for drift on startup, not just when you open the
  Verify page. If a tweak it applied has quietly reverted to the Windows
  default — most often after a Windows Update — a notification appears at
  the top of the content area letting you know. "Review" takes you
  straight to the Verify page to re-apply or dismiss it.
- The check runs after the tweak catalog has finished loading, so every
  tracked tweak is accounted for, and it stays hidden when nothing has
  drifted.

### Windows Apps & External Apps

- Fixed app cards being clipped along the bottom. Cards with a longer
  description and a status pill (Installed, Warning, or Permanent) were
  having their bottom edge and rounded corners cut off. Cards now size to
  fit their content, so nothing is hidden. Column count and card width are
  unchanged.

### Interface

- The "Recommended" badge on a tweak row is easier to read. It no longer
  sits on a solid red fill that washed out its label — it's now an outline
  pill, matching the "Windows Default" and "Preference" badges next to it.

## v2.0.1

### AkariOS

- Added "AkariOS Playbook Services" — available only on machines actually
  built from the AkariOS AME Playbook. Lets you apply whichever
  service-startup list the playbook shipped (found automatically in
  C:\PostInstall\Services) directly from Akari Tool, instead of running
  it separately. Remembers the last list you picked.

### Customize

- Customize is now organized like Advanced Tools: one entry in the
  sidebar, with Taskbar, Explorer, Context Menu, Appearance, Start Menu,
  and Desktop reachable as cards from the page itself — instead of one
  long scrolling list of every tweak in every category.
- Global search still jumps straight to the exact tweak inside its
  category page, wherever it lives.

### New: Verify

- Added a Verify page. It checks every tweak Akari Tool has applied
  against what your system currently reports, and flags anything that no
  longer matches — most often the signature of a Windows Update quietly
  reverting a setting.
- Tweaks reverted to the Windows default can be re-applied individually or
  all at once. Tweaks changed to something else entirely are called out
  separately and reviewed one at a time, since that's more likely
  something you changed on purpose.
- Any flagged tweak can be dismissed permanently if the change was
  intentional.

### Interface

- Replaced the title-bar logo with a vector mark. It's crisp at any
  display scale, and follows light/dark theme automatically instead of
  relying on separate light/dark image assets.
- Redesigned the Home page's system info card: Windows edition and build
  now sit as a header above a Processor / Graphics / Memory summary.
