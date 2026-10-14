using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroArchive {
    // One step of a guided walkthrough. Target names the control to spotlight, Page the page to
    // show (-1 keeps the current page), Do the optional action button and Check a live test of
    // the user's setup shown with the step. Do and Check are keys the main window interprets.
    public sealed class WalkthroughStep {
        public string Title, Body, Target, Action, Do, Check;
        public int Page = -1;
    }
    // A walkthrough is either a tour of a workflow or a guided fix for one problem.
    public sealed class WalkthroughGuide {
        public string Id, Title, Summary, Keywords;
        public bool Fix;
        public WalkthroughStep[] Steps;
        public override string ToString() { return Title; }
    }
    public static class WalkthroughCatalog {
        public const string Tour = "tour", FirstImport = "first-import";
        static WalkthroughStep S(string title, int page, string target, string body, string action = null, string act = null, string check = null) {
            return new WalkthroughStep { Title = title, Page = page, Target = target, Body = body, Action = action, Do = act, Check = check };
        }
        // Every guided fix ends by offering a problem report with the diagnostics attached.
        static WalkthroughStep StillStuck(string target) {
            return S("Still stuck?", -1, target, "If the steps above didn't solve it, send a problem report.\n\n- AstroArchive fills in your version, system details and recent log.\n- You review everything before GitHub opens; nothing is sent automatically.", "Report a problem…", "report");
        }
        static WalkthroughStep[] TourSteps() {
            return new[] {
                S("Switch pages from the toolbar", -1, "PageSelector", "Use the purple **PAGE** selector to move around AstroArchive. It sits beside the toolbar, or below it in smaller windows.\n\n- Choose **Repository**, **Edited** or **Import**.\n- Search, filters, selections and import progress stay in place.\n- Use `Ctrl+1–3` to select a page or `Ctrl+Tab` to cycle pages.", "Try the page selector", "page-selector"),
                S("Choose your repository", 0, "OpenRepositoryFolderButton", "Your repository holds verified capture copies and the archive index.\n\n1. Open **Settings → General**.\n2. Choose your archive folder.\n\nClick the repository path on the main page to open it in Explorer.", "Open Preferences", "preferences:0", "repository"),
                S("Select a telescope or source folder", 1, "ImportSourceCard", "Select a saved telescope or **Browse** its capture folder. Give each physical telescope its own device ID.\n\n- **Import from Seestar… / Dwarflab…** selects USB folders/files and confirms a full scan and import. Choose target folders for faster scans.\n- **Telescopes** saves, renames or recovers profiles.\n- **Scan folder** skips matching filenames and known DWARF sessions; Seestar scans still find new filenames.\n- Enable **Robust file matching** in Import options to revisit edits and additions inside skipped sessions.\n- **Review and repair → Full rescan** reads and hashes every file for a complete check.", "Open Import menu", "menu:ImportMenu", "source"),
                S("Review before importing", 1, "ImportGrid", "Check each file’s **status** and **review reason** in the scan table.\n\n- **Review and repair** opens flagged captures and transfer retries.\n- **Import options** controls analysis and original removal.\n- The policy summary stays visible. **Originals are kept by default.**", "Open review and import options", "menu:ImportMenu", "scanned"),
                S("Import verified copies", 1, "ImportButton", "Import copies ready files from the **filtered scan** and verifies their checksums.\n\n- Already archived content is skipped.\n- Progress and timing appear during the job.\n- **Cancel** stops safely after the current operation.\n\nThis walkthrough never starts an import for you.", "Show import actions", "menu:ImportMenu", "ready"),
                S("Browse, preview and sort", 0, "FramesGrid", "Browse targets and sessions, or narrow the view with **Search** and **Filters**.\n\n## Select and organise\n- `Ctrl/Shift` selects files; right-click opens file actions.\n- **Group subs by session** (Settings) switches between compact sessions and files.\n\n## Sort and choose columns\n- Click a heading to sort; click again to reverse. `Shift-click` adds a heading.\n- ▲ / ▼ show direction; numbers show sort priority.\n- Right-click a heading to choose columns.", "Open Repository view options", "menu:RepositoryMenu"),
                S("Export a stacking project", 0, "ExportButton", "Select captures, then choose **Export**: copy originals, prepare a ready-to-stack folder, open them in an app, or create an AstroBin acquisition CSV.\n\n- Stacks copy to the destination; subs keep compatible input folders.\n- Matching calibrations include reasons; scientific conversion is explicit.\n- **Add Metadata** and **Create new folder** start off. Existing files are kept.\n\nStacking takes place in your chosen processing software.", "Open Export menu", "menu:ExportMenu"),
                S("Make AstroArchive comfortable", 0, "SettingsMenu", "Open **Settings** for Preferences.\n\n- **General** sets appearance and the repository.\n- **Accessibility** adjusts text, rows, contrast and motion.\n- **Guide** opens searchable help, this walkthrough and About AstroArchive.\n- Press `F1` for help on the current page.", "Open accessibility preferences", "preferences:4")
            };
        }
        static WalkthroughGuide[] guides;
        public static WalkthroughGuide[] All() {
            if (guides != null) return guides;
            var tour = TourSteps();
            guides = new[] {
                new WalkthroughGuide { Id = Tour, Title = "AstroArchive walkthrough", Summary = "A quick tour of pages, importing, browsing, exporting and settings.", Keywords = "tour introduction overview", Steps = tour },
                new WalkthroughGuide { Id = FirstImport, Title = "Your first import", Summary = "Choose a repository, pick a source, scan it and import verified copies.", Keywords = "start begin new setup", Steps = new[] { tour[1], tour[2], tour[3], tour[4] } },
                new WalkthroughGuide { Id = "export-stacking", Title = "Export captures for stacking", Summary = "Pick subs, prepare a ready-to-stack folder and open it in your stacking app.", Keywords = "siril dss stack calibration export", Steps = new[] {
                    S("Pick the captures", 0, "TargetList", "Choose a target on the left, then select the subs you want in the table.\n\n- `Ctrl`-click adds files; `Shift`-click selects a range.\n- Selecting a session row includes all of its subs.\n- The counter at the top right shows how many files are selected.", null, null, "selection"),
                    S("Prepare a stacking folder", 0, "ExportButton", "Choose **Export**, then **Stacking folder**.\n\n- **Subs**, **Stacks** or **Both** decides what is copied.\n- Matching calibration frames are offered with the reason they match.\n- Existing files in the destination are never overwritten.", "Open Export", "export"),
                    S("Open it in your stacking app", 0, "SettingsMenu", "**Open in app** can hand the exported files to Siril, DSS or another stacker.\n\n- Set application locations under **Settings → Export**.\n- AstroArchive never starts stacking for you.", "Open export preferences", "preferences:2") } },
                new WalkthroughGuide { Id = "backup", Title = "Back up your archive", Summary = "Make a verified copy on another drive and keep it up to date automatically.", Keywords = "backup restore copy drive protect", Steps = new[] {
                    S("Make your first backup", 0, "RepositoryMenu", "Choose **Repository → Back up archive…** and pick a folder on a separate drive.\n\n- A folder keeps later backups small; a ZIP is one file.\n- Close editors that write into the archive first.\n- Cancelling removes the unfinished backup and keeps earlier ones.", "Back up archive…", "backup", "backup"),
                    S("Repeat it automatically", 0, "SettingsMenu", "After a backup, AstroArchive offers to repeat it daily, weekly or monthly.\n\n- Change the schedule in **Settings → Backups**.\n- If the backup drive is missing when one is due, Activity reminds you.", "Open backup preferences", "preferences:6"),
                    S("Protect your originals", 0, "SettingsMenu", "**Settings → Backups → Protect originals** prevents accidental deletion or renaming on local NTFS drives.\n\n> Note: Protection is not a backup. Turn it off before moving the archive in Explorer.", "Open backup preferences", "preferences:6") } },
                new WalkthroughGuide { Id = "import-disabled", Fix = true, Title = "Import button is greyed out", Summary = "Checks each thing Import needs and takes you to whatever is missing.", Keywords = "disabled unavailable grey gray cannot import scan", Steps = new[] {
                    S("Choose a repository", 1, "SettingsMenu", "Import copies files into your repository, so one must be chosen first.\n\n1. Open **Settings → General**.\n2. Choose your archive folder and save.", "Open Preferences", "preferences:0", "repository"),
                    S("Let other work finish", 1, "ActivityBell", "Only one archive job runs at a time. A scan, import, backup or analysis keeps Import unavailable until it ends.\n\n- The bell opens **Activity**, which shows what is running.\n- Wait for it to finish, or pause or cancel it there.", "Open Activity", "activity", "idle"),
                    S("Pick a source", 1, "ImportSourceCard", "Select a saved telescope, or **Browse** to the folder your captures are in.\n\n- A USB telescope appears as **Import from Seestar…** or **Import from Dwarflab…** in the Import menu.\n- For Wi-Fi, use **Connect over network** instead.", "Browse for a folder…", "browse-source", "source"),
                    S("Scan the source", 1, "ScanButton", "**Scan folder** reads the source and lists what can be imported. Nothing is copied yet.\n\n- Files already in the archive are skipped.\n- A large folder can take a while; progress shows in Activity.", "Scan folder", "scan", "scanned"),
                    S("Make sure files are ready", 1, "ImportGrid", "Import copies the **ready** files in the current view. If none are ready:\n\n- Clear the Import search box and **Filters**.\n- Check the **Status** and **Review** columns: duplicates and failed files are skipped.\n- **Import → Review and repair** shows flagged captures.", "Clear search and filters", "clear-import-search", "ready"),
                    StillStuck("ImportButton") } },
                new WalkthroughGuide { Id = "network-telescope", Fix = true, Title = "Telescope not found over Wi-Fi", Summary = "Get discovery working, or connect by IP address, for Seestar, DWARF and Origin.", Keywords = "wifi wi-fi lan network discovery seestar dwarf origin connect ip hotspot", Steps = new[] {
                    S("Open the network connection", 1, "RemoteTelescopeButton", "Choose **Connect over network** beside Saved telescope. Discovery starts by itself; no IP address is needed for Seestar, DWARF or Celestron Origin.", "Connect over network…", "connect-network", "repository"),
                    S("Use the same network", 1, "RemoteTelescopeButton", "- Wake the telescope and connect it to your home Wi-Fi, or join the telescope's own hotspot from this PC.\n- Guest Wi-Fi and client isolation block discovery.\n- Click **Search** again after changing networks."),
                    S("Enter the address yourself", 1, "RemoteTelescopeButton", "If discovery still finds nothing, open **Advanced** in the connection window.\n\n1. Find the telescope's IP address in its app or your router's device list.\n2. Enter it as the IPv4 address and leave **Storage path** blank.\n\n> Note: Use the telescope's address, not this PC's.", "Connect over network…", "connect-network"),
                    S("Check the telescope keeps frames", 1, "RemoteTelescopeButton", "- **Seestar:** turn on **Save Each Frame** and use current firmware.\n- **Celestron Origin:** turn on **Save Raw Images** (Menu → Settings → Advanced).\n- **DWARF:** check the FTP port and credentials under **Advanced**."),
                    StillStuck("RemoteTelescopeButton") } },
                new WalkthroughGuide { Id = "usb-telescope", Fix = true, Title = "USB telescope isn't detected", Summary = "Make the telescope show up as a drive, refresh devices, or browse to it directly.", Keywords = "usb cable drive seestar dwarf missing not recognised recognized", Steps = new[] {
                    S("Connect it as a drive", 1, "ImportSourceCard", "Connect the telescope by USB and check that it appears as a drive in **File Explorer**.\n\n- Some telescopes need USB storage mode turned on in their app.\n- Wake or unlock the telescope if Windows shows no drive.", null, null, "usb"),
                    S("Refresh USB devices", 1, "ImportSourceCard", "Choose **Import → Telescopes → Refresh USB devices**. A recognised telescope then appears as **Import from Seestar…** or **Import from Dwarflab…** in the Import menu.", "Refresh USB devices", "refresh-usb", "usb"),
                    S("Or browse to the drive", 1, "SourceButton", "If it still isn't recognised, **Browse** to the capture folder on the telescope drive and scan it like any folder. Save it as a telescope so it's remembered.", "Browse for a folder…", "browse-source", "source"),
                    StillStuck("ImportSourceCard") } },
                new WalkthroughGuide { Id = "missing-files", Fix = true, Title = "My files don't show in Repository", Summary = "Find captures hidden by search, filters, the target list or grouping.", Keywords = "missing empty no rows hidden gone disappeared files captures", Steps = new[] {
                    S("Check the right archive is open", 0, "RepositoryPathStrip", "The strip under the table shows which repository is open. If it's the wrong one, choose another in **Settings → General**.", "Open Preferences", "preferences:0", "repository"),
                    S("Clear search and filters", 0, "SearchBox", "A search term or active **Filters** hide everything that doesn't match.\n\n- **Clear** empties the search and resets filters.\n- The Filters button shows how many are active, for example **Filters (2)**.", "Clear search and filters", "clear-search", "unfiltered"),
                    S("Choose All Targets", 0, "TargetList", "The target list on the left limits the table to one object. **All Targets**, at the top, shows everything.", "Select All Targets", "all-targets", "all-targets"),
                    S("Expand sessions", 0, "FramesGrid", "Subs are grouped into one row per session. Expand a row, or turn off **Group subs by session** in Settings, to see every file.", "Open Repository menu", "menu:RepositoryMenu"),
                    S("Import the files", 0, "RepositoryImportButton", "If the archive is empty, the files haven't been imported yet. Go to **Import**, scan the folder and import.", "Go to Import", "page:1", "has-files"),
                    StillStuck("FramesGrid") } },
                new WalkthroughGuide { Id = "preview-blank", Fix = true, Title = "Preview shows no image", Summary = "Turn the preview on, select one file and check the format is supported.", Keywords = "preview blank image unavailable raw codec cr2 nef black", Steps = new[] {
                    S("Turn the preview on", 0, "FramesGrid", "The preview pane can be hidden. Turn on **Show image preview** in **Settings → General**.", "Open Preferences", "preferences:0", "preview-on"),
                    S("Select one file", 0, "FramesGrid", "Select a single file in the table. With nothing selected, the pane shows information about the target instead.", null, null, "one-file"),
                    S("Check the file format", 0, "PreviewPane", "- FITS, XISF, TIFF, PNG, JPEG and SER preview directly.\n- RAW camera files (CR2, NEF, ARW, DNG) need a Windows image codec.\n- Tile-compressed FITS needs the optional CFITSIO codec.\n\n**Settings → General → Image compatibility** shows what is installed.", "Open Preferences", "preferences:0"),
                    StillStuck("PreviewPane") } },
                new WalkthroughGuide { Id = "app-missing", Fix = true, Title = "Open in app can't find my software", Summary = "Point AstroArchive at Siril, DSS, PixInsight or another app installed somewhere else.", Keywords = "application siril dss pixinsight photoshop missing not found open in app exe", Steps = new[] {
                    S("Set the app's location", 0, "SettingsMenu", "AstroArchive looks for common processing apps where they normally install. If yours is elsewhere:\n\n1. Open **Settings → Export**.\n2. Under **Application locations**, choose the program's main `.exe`.\n3. Save preferences.", "Open export preferences", "preferences:2"),
                    S("Try it again", 0, "ExportButton", "Select files, then choose **Export → Open in app…**. Copies are verified before the app opens them; your originals are never changed.", "Open Export menu", "menu:ExportMenu"),
                    StillStuck("ExportButton") } },
                new WalkthroughGuide { Id = "sky-wrong", Fix = true, Title = "Sky, Moon or eclipse details look wrong", Summary = "Set where and when captures were taken so heights, phases and eclipses are right.", Keywords = "location town city timezone time clock moon sun eclipse horizon altitude wrong", Steps = new[] {
                    S("Set your observing town", 0, "SettingsMenu", "Sky details depend on where and when a capture was taken. Open **Settings → Sky & solving** and search for your town, or enter coordinates. The first-run **Where do you observe from?** window sets the same thing.", "Open sky preferences", "preferences:3", "location"),
                    S("Fix individual captures", 0, "FramesGrid", "If only some captures are wrong, select them and right-click **Edit metadata → Capture**.\n\n- Set the **town / city** where they were taken.\n- Set the **timezone** if the camera clock wasn't on local time.\n- Changes are saved with the archive; files are never rewritten."),
                    S("Check the details again", 0, "PreviewDetailsButton", "Select a capture and open the **i** button above the preview. Target and Moon heights, Moon phase and eclipse details update straight away."),
                    StillStuck("PreviewDetailsButton") } },
                new WalkthroughGuide { Id = "identify-targets", Fix = true, Title = "Captures show an Unknown target", Summary = "Plate solve captures, or name the target yourself.", Keywords = "unknown target plate solve astap astrometry identify name", Steps = new[] {
                    S("Set up a plate solver", 0, "SettingsMenu", "Plate solving works out where in the sky an image points. Open **Settings → Sky & solving → Plate solver setup** and choose **ASTAP** with its star database, or **Astrometry.net** with an API key.", "Open sky preferences", "preferences:3", "solver"),
                    S("Select the captures", 0, "FramesGrid", "Select the Unknown captures, or a whole session row. One frame per session is solved and the result applies to the session.", null, null, "selection"),
                    S("Identify targets", 0, "RepositoryMenu", "Choose **Repository → Review and analysis → Identify targets…**. Review the matches before applying them; cancelling keeps what's already done.", "Open Repository menu", "menu:RepositoryMenu"),
                    S("Or name it yourself", 0, "FramesGrid", "If you know the target, right-click **Edit metadata** and type it. Names such as M31, NGC 7000 or Andromeda Galaxy are all recognised."),
                    StillStuck("FramesGrid") } }
            };
            return guides;
        }
        public static WalkthroughGuide Find(string id) { return All().FirstOrDefault(g => g.Id == id); }
        public static IEnumerable<WalkthroughGuide> Fixes() { return All().Where(g => g.Fix); }
        public static IEnumerable<WalkthroughGuide> Tours() { return All().Where(g => !g.Fix && g.Id != FirstImport); }
        public static List<WalkthroughGuide> Search(string query) {
            var words = Util.Tokens(query);
            if (words.Length == 0) return new List<WalkthroughGuide>();
            return All().Where(g => g.Id != FirstImport && words.All(w => (g.Title + " " + g.Summary + " " + g.Keywords).IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }
    }
}
