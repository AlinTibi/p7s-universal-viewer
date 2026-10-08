# Source import

The original C# / XAML files were imported in a signed commit before modernization. Their original state remains accessible in Git history. Generated output, IDE state, user settings and documents were excluded. The original source and distribution folders were not modified.

The new WPF application replaces the original monolithic UI verification logic with a testable CMS core. It keeps local content extraction and preview workflows, while removing unused Office/archive parsers and donation pop-ups. Historical static Pages files remain at repository root so their existing URLs do not change.
