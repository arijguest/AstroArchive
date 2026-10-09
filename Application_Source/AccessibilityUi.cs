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
            Window.Resources["UiCheckSize"] = 18.0 * scale;
            Window.Resources["UiToolbarIconSize"] = 16.0 * scale;
            Window.Resources["PreferHighContrast"] = settings.HighContrast;
            Theme.Apply(Window, settings.ThemeMode);
            if (navigationReady) UpdateCompactHeader();SizeActivity();
            foreach (string name in new[] { "FramesGrid", "ImportGrid", "EditedGrid", "MetricsGrid" }) {
                var table = Window.FindName(name) as DataGrid;
                if (table != null) TableSortIndicators.SizeColumns(table, scale);
            }
        }
        AccessibilityChoices AddAccessibilityPreferences(FormWindow dialog) {
            dialog.Text("Accessibility", true);
            dialog.Text("Adjust text and table spacing without scaling images. Settings apply to this Windows user and remain in place after updates.");
            int percent = settings.TextScalePercent == 0 ? 100 : settings.TextScalePercent;
            var choices = new AccessibilityChoices {
                Scale = dialog.Select("Text size", new[] { "100%", "115%", "130%", "150%" }, percent + "%"),
                Rows = dialog.Check("Comfortable table rows", settings.ComfortableRows),
                Contrast = dialog.Check("High contrast surfaces and text", settings.HighContrast),
                Motion = dialog.Check("Reduce motion and indeterminate progress animation", settings.ReducedMotion)
            };
            UiHelp.Describe(choices.Scale, "Increase text size without scaling images.");
            UiHelp.Describe(choices.Rows, "Use taller table rows.");
            UiHelp.Describe(choices.Contrast, "Use high-contrast colours.");
            UiHelp.Describe(choices.Motion, "Disable progress animation.");
            dialog.Advanced("Keyboard shortcuts",()=>{
            dialog.Text("Alt reveals menu access keys. Arrow keys move through dropdowns; Escape closes them. Tab follows the visible controls, with clear focus outlines. The purple page selector switches pages; Ctrl+1–3 selects Repository, Edited or Import, and Ctrl+Tab cycles pages. Ctrl+F focuses search, Ctrl+I opens Import, Ctrl+E opens Export, and F1 opens help for the current page.");
            dialog.Text("Table sorting uses ▲ for ascending and ▼ for descending. Numbers show the order of multi-column sorting. Shift-click a heading adds it to the sort.");
            });
            return choices;
        }
    }
}
