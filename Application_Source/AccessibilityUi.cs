using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AstroArchive {
    public sealed class AccessibilityChoices {
        public ComboBox Scale;
        public CheckBox Rows, Contrast, Motion;
        public void Save(Settings settings) {
            int scale;
            settings.TextScalePercent = int.TryParse(Convert.ToString(Scale.SelectedItem).TrimEnd('%'), out scale) ? scale : 100;
            settings.ComfortableRows = Rows.IsChecked == true;
            settings.HighContrast = Contrast.IsChecked == true;
            settings.ReducedMotion = Motion.IsChecked == true;
        }
    }
    public partial class MainUi {
        void ApplyAppearance() {
            int percent = new[] { 100, 115, 130, 150 }.Contains(settings.TextScalePercent) ? settings.TextScalePercent : 100;
            double scale = percent / 100.0;
            string[] names = { "UiFontCaption", "UiFontSmall", "UiFontBody", "UiFontControl", "UiFontHeading", "UiFontTitle", "UiFontBrand", "UiFontHeaderBrand" };
            double[] sizes = { 10, 11, 12, 13, 16, 18, 20, 26 };
            for (int i = 0; i < names.Length; i++) Window.Resources[names[i]] = sizes[i] * scale;
            Window.Resources["CaptureRowHeight"] = (settings.ComfortableRows ? 44.0 : 32.0) * scale;
            Window.Resources["PreferHighContrast"] = settings.HighContrast;
            Theme.Apply(Window, settings.ThemeMode);
            foreach (string name in new[] { "FramesGrid", "ImportGrid", "MetricsGrid", "MosaicGrid" }) {
                var table = Window.FindName(name) as DataGrid;
                if (table != null) TableSortIndicators.SizeColumns(table, scale);
            }
        }
        AccessibilityChoices AddAccessibilityPreferences(FormWindow dialog) {
            dialog.Tab(4);
            dialog.Text("Accessibility", true);
            dialog.Text("Adjust text and table spacing without scaling images. Settings apply to this Windows user and remain in place after updates.");
            int percent = settings.TextScalePercent == 0 ? 100 : settings.TextScalePercent;
            var choices = new AccessibilityChoices {
                Scale = dialog.Select("Text size", new[] { "100%", "115%", "130%", "150%" }, percent + "%"),
                Rows = dialog.Check("Comfortable table rows", settings.ComfortableRows),
                Contrast = dialog.Check("High contrast surfaces and text", settings.HighContrast),
                Motion = dialog.Check("Reduce motion and indeterminate progress animation", settings.ReducedMotion)
            };
            UiHelp.Tip(choices.Scale, "Increase interface text, including menu, header and status text. Image pixels remain unchanged.");
            UiHelp.Tip(choices.Rows, "Give capture rows more height for easier reading and pointer selection.");
            UiHelp.Tip(choices.Contrast, "Use clear monochrome surfaces in your chosen light or dark theme. Windows high contrast is also respected automatically.");
            UiHelp.Tip(choices.Motion, "Keep progress updates readable while stopping the indeterminate progress animation. Operations continue normally.");
            dialog.Text("Keyboard access", true);
            dialog.Text("Alt reveals menu access keys. Arrow keys move through dropdowns; Escape closes them. Tab follows the visible controls, with clear focus outlines. Ctrl+F focuses search, Ctrl+I opens Import, Ctrl+E opens Export, and F1 opens help for the current page.");
            dialog.Text("Table sorting uses ▲ for ascending and ▼ for descending. Numbers show the order of multi-column sorting. Shift-click a heading adds it to the sort.");
            return choices;
        }
    }
}
