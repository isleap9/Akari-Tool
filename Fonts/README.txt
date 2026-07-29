AKARI TOOL — EMBEDDED FONTS
===========================

The redesign uses three Google Fonts. Drop their .ttf files in THIS folder and they
embed into the build automatically (AkariTool.csproj has <Resource Include="Fonts\*.ttf" />).
Until the files are present, the app falls back to Segoe UI / Consolas — nothing breaks.

DOWNLOAD (free, OFL license — redistributable inside your app):
  • Space Grotesk  — https://fonts.google.com/specimen/Space+Grotesk
  • Manrope        — https://fonts.google.com/specimen/Manrope
  • JetBrains Mono — https://fonts.google.com/specimen/JetBrains+Mono

WHICH FILES (the static .ttf weights — NOT the variable-font versions):
  SpaceGrotesk-Regular.ttf   SpaceGrotesk-Medium.ttf   SpaceGrotesk-SemiBold.ttf   SpaceGrotesk-Bold.ttf
  Manrope-Regular.ttf        Manrope-Medium.ttf        Manrope-SemiBold.ttf        Manrope-Bold.ttf
  JetBrainsMono-Regular.ttf  JetBrainsMono-Medium.ttf

The internal family names must read exactly "Space Grotesk", "Manrope", "JetBrains Mono"
(the Google ttf files already have these). App.xaml references them via:
  DisplayFont = Space Grotesk   (headings/title)
  BodyFont    = Manrope         (default UI text — set on the Window)
  MonoFont    = JetBrains Mono  (mono labels/console)

After adding the files: rebuild. No code changes needed.
