# Guide and tooltip validation

The main window's 40 interactive controls have descriptive tooltip entries.
Tooltip coverage also includes sortable table headers, file-menu actions, preview
controls and key metadata, export and settings options. Disabled controls retain
their tooltips. Main control and dialog tooltips expose their text to accessibility
tools through `AutomationProperties.HelpText`.

Guide opens a menu of common topics and a searchable, resizable two-pane window.
The guide has 20 topics, including Import/library workflows, troubleshooting,
keyboard shortcuts and a frame glossary. F1 opens the current tab's help;
Ctrl+F focuses help search. Search checks every entered word against topic titles
and full text; empty results explain how to recover. Users can select/copy text
or save the complete guide. The window reads the embedded `Quick_Start.txt`, so
help is available offline and uses the same content as the packaged guide.

The complete application and console suite compile without warnings as C# 5
against Microsoft .NET Framework 4.8 reference assemblies. Generated-data
regressions ran under Mono with Linux SQLite: **85 passed, 5 Windows-only tests
skipped**. Help tests cover embedded topic availability, preserved paragraphs,
case-insensitive multiword search, empty search and no matching results. XAML XML
validity, tooltip control names/coverage, and the Import tab label were checked.

The Windows `--ui-test` routine checks tooltip presence, the Import label,
contextual topic selection, preview-topic search, no results and clearing search.
It compiled but could not run on Linux. On Windows, run
`Application_Source/test.ps1` and `scripts/build-release.ps1`; verify hover over
enabled/disabled controls, Guide-menu navigation, F1 from both tabs, Ctrl+F,
keyboard topic selection, scrolling/resizing and saving the guide.
