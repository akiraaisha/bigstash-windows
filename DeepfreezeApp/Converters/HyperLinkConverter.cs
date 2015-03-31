using DeepfreezeModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace BigStash.WPF
{
    public class HyperLinkConverter : IMultiValueConverter
    {
        // The following regural expression identifies href links in strings. 
        // The href have to be written in the string like this:
        // [a href='http://example.com']Example Site[/a]
        private Regex regex = new Regex(@"\[a\s+href='(?<link>[^']+)'\](?<text>.*?)\[/a\]", RegexOptions.Compiled);

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string originalText = (string)values[0];
            TextBlock tb = (TextBlock)values[1];

            tb.Inlines.Clear();

            foreach (Match match in regex.Matches(originalText))
            { 
                string link    = match.Groups["link"].Value;
                int link_start = match.Groups["link"].Index;
                int link_end   = match.Groups["link"].Index + link.Length;

                string text    = match.Groups["text"].Value;
                int text_start = match.Groups["text"].Index;
                int text_end   = match.Groups["text"].Index + text.Length;

                int matchIndex = originalText.IndexOf(match.Value);

                // add the text part from the previous link (if any) up to the start of the current link
                tb.Inlines.Add(originalText.Substring(0, matchIndex));

                // get the index where the current match ends
                int matchEnd = matchIndex + match.Value.Length;

                // remove the written part of the originalText.
                originalText = originalText.Remove(0, matchEnd);

                Run r = new Run(text);
                Hyperlink lnk = new Hyperlink(r);
                lnk.NavigateUri = new Uri(link);
                lnk.Click += Hyperlink_Click;
                tb.Inlines.Add(lnk);
            }
            // 0123456789
            tb.Inlines.Add(originalText.Substring(0));

            return null;
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(((Hyperlink)sender).NavigateUri.ToString());
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
