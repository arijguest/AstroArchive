using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;

namespace AstroArchive {
    public static class TableSortIndicators {
        public static readonly DependencyProperty MarkProperty = DependencyProperty.RegisterAttached("Mark", typeof(string), typeof(TableSortIndicators), new PropertyMetadata(""));
        static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(TableSortIndicators), new PropertyMetadata(false));
        static readonly DependencyProperty BaseMinimumProperty = DependencyProperty.RegisterAttached("BaseMinimum", typeof(double), typeof(TableSortIndicators), new PropertyMetadata(double.NaN));
        public static string GetMark(DependencyObject element) { return (string)element.GetValue(MarkProperty); }
        public static void SetMark(DependencyObject element, string value) { element.SetValue(MarkProperty, value); }
        public static void SizeColumns(DataGrid grid, double scale) {
            foreach (var column in grid.Columns) {
                double minimum = (double)column.GetValue(BaseMinimumProperty);
                if (double.IsNaN(minimum)) {
                    minimum = Math.Max(column.MinWidth, column.Width.IsAbsolute ? column.Width.Value : column.Width.IsStar ? 120 : 20);
                    column.SetValue(BaseMinimumProperty, minimum);
                }
                var caption = new TextBlock { Text = System.Convert.ToString(column.Header), FontFamily = grid.FontFamily, FontSize = 10 * scale, FontWeight = FontWeights.SemiBold };
                caption.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                // A star column otherwise lets WPF squeeze pixel columns down to 20 px.
                // Preserve the caption and room for direction/priority; scroll the table.
                column.MinWidth = Math.Max(minimum * scale, caption.DesiredSize.Width + 48);
            }
        }
        public static void Update(DataGrid grid,System.Collections.Generic.IEnumerable<SortDescription> preparedSorts=null) {
            var sorts = (preparedSorts??grid.Items.SortDescriptions).ToList();
            foreach (var column in grid.Columns) {
                int index = sorts.FindIndex(sort => sort.PropertyName == column.SortMemberPath);
                string mark = index < 0 ? "" : (sorts[index].Direction == ListSortDirection.Ascending ? "▲" : "▼") + (sorts.Count > 1 ? " " + (index + 1) : "");
                SetMark(column, mark);
                var style = column.HeaderStyle;
                // Header content stays a plain caption, preserving menus and automation names.
                if (style == null) {
                    style = new Style(typeof(DataGridColumnHeader), grid.TryFindResource(typeof(DataGridColumnHeader)) as Style);
                    column.HeaderStyle = style;
                }
            }
        }
        public static void Attach(DataGrid grid) {
            if ((bool)grid.GetValue(AttachedProperty)) return;
            grid.SetValue(AttachedProperty, true);
            var font = grid.TryFindResource("UiFontCaption");
            SizeColumns(grid, font is double ? (double)font / 10.0 : 1.0);
            foreach (var column in grid.Columns) {
                var binding = (column as DataGridBoundColumn) == null ? null : ((DataGridBoundColumn)column).Binding as Binding;
                if (string.IsNullOrEmpty(column.SortMemberPath) && binding != null && binding.Path != null) column.SortMemberPath = binding.Path.Path;
                var style = new Style(typeof(DataGridColumnHeader), column.HeaderStyle ?? grid.TryFindResource(typeof(DataGridColumnHeader)) as Style);
                var description = new MultiBinding { Converter = new SortHeaderDescriptionConverter() };
                description.Bindings.Add(new Binding("Column.Header") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) });
                description.Bindings.Add(new Binding { Path = new PropertyPath("Column.(0)", MarkProperty), RelativeSource = new RelativeSource(RelativeSourceMode.Self) });
                style.Setters.Add(new Setter(AutomationProperties.NameProperty, description));
                column.HeaderStyle = style;
            }
            grid.Sorting += (s,e) => grid.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(() => Update(grid)));
            Update(grid);
        }
    }
    public sealed class SortHeaderDescriptionConverter : IMultiValueConverter {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            string label = values.Length > 0 ? System.Convert.ToString(values[0]) : "Column";
            string mark = values.Length > 1 ? System.Convert.ToString(values[1]) : "";
            if (string.IsNullOrEmpty(mark)) return label + ", not sorted. Activate to sort ascending.";
            return label + ", sorted " + (mark.StartsWith("▲") ? "ascending" : "descending") + (mark.Length > 1 ? ", priority " + mark.Substring(1).Trim() : "") + ". Activate to reverse sorting.";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture) { throw new NotSupportedException(); }
    }
}
