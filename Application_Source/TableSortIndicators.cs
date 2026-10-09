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
        static readonly DependencyProperty PreparedSortsProperty = DependencyProperty.RegisterAttached("PreparedSorts", typeof(System.Collections.Generic.IEnumerable<SortDescription>), typeof(TableSortIndicators), new PropertyMetadata(null));
        static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(TableSortIndicators), new PropertyMetadata(false));
        public static string GetMark(DependencyObject element) { return (string)element.GetValue(MarkProperty); }
        public static void SetMark(DependencyObject element, string value) { element.SetValue(MarkProperty, value); }
        public static void SizeColumns(DataGrid grid, double scale) {
            foreach (var column in grid.Columns) {
                // Keep a usable gripper without forcing the caption's full width.
                // Captions/cells clip; explicit resizing may overflow the viewport.
                column.MinWidth = 32 * scale;
            }
        }
        public static void Update(DataGrid grid,System.Collections.Generic.IEnumerable<SortDescription> preparedSorts=null) {
            if(preparedSorts!=null)grid.SetValue(PreparedSortsProperty,preparedSorts.ToList());
            var sorts = (preparedSorts??(System.Collections.Generic.IEnumerable<SortDescription>)grid.GetValue(PreparedSortsProperty)??grid.Items.SortDescriptions).ToList();
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
            TableColumnResizing.Attach(grid);
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
