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
