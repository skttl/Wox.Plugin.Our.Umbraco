using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace Wox.Plugin.Our.Umbraco
{
    internal class Main : IPlugin
    {
        public Main()
        {
        }

        public void Init(PluginInitContext context)
        {
        }

        public string RelativeDate(DateTime yourDate)
        {
            const int SECOND = 1;
            const int MINUTE = 60 * SECOND;
            const int HOUR = 60 * MINUTE;
            const int DAY = 24 * HOUR;
            const int MONTH = 30 * DAY;

            var ts = new TimeSpan(DateTime.UtcNow.Ticks - yourDate.Ticks);
            double delta = Math.Abs(ts.TotalSeconds);

            if (delta < 1*MINUTE)
            {
                return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago";
            }

            if (delta < 2 * MINUTE)
            {
                return "a minute ago";
            }

            if (delta < 45 * MINUTE)
            {
                return ts.Minutes + " minutes ago";
            }

            if (delta < 90 * MINUTE)
            {
                return "an hour ago";
            }

            if (delta < 24 * HOUR) {
                return ts.Hours + " hours ago";
            }

            if (delta < 48 * HOUR)
            {
                return "yesterday";
            }

            if (delta < 30 * DAY) {
                return ts.Days + " days ago";
            }

            if (delta < 12 * MONTH)
            {
                int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                return months <= 1 ? "one month ago" : months + " months ago";
            }
            else
            {
                int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                return years <= 1 ? "one year ago" : years + " years ago";
            }
        }

        public DateTime StringToDate(string date)
        {
            try
            {
                return DateTime.ParseExact(date, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                return DateTime.Now;
            }
        }

        public string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }

        public string Truncate(string source, int length)
        {
            if (source.Length > length)
            {
                source = source.Substring(0, length) + "...";
            }
            var result = Regex.Replace(source, @"\r\n?|\n", " ");
            return result;
        }

        public string GenerateSubTitle(JToken fields)
        {
            try
            {
                var subtitle = "";
                switch (fields["nodeTypeAlias"].ToString())
                {
                    case "forum":
                        subtitle = "Posted " + RelativeDate(StringToDate(fields["createDate"].ToString())) + " by: " +
                                       fields["authorName"];

                        if (fields["replies"].ToString() != "0")
                        {
                            subtitle = subtitle + " - " + fields["replies"] + " replies, last reply " +
                                       RelativeDate(StringToDate(fields["createDate"].ToString())) + " by " +
                                       fields["latestReplyAuthorName"];
                        }
                        else
                        {
                            subtitle = subtitle + " - No replies";
                        }
                        subtitle = subtitle + Environment.NewLine + Truncate(fields["body"].ToString(), 140);
                        break;
                    case "project":
                        subtitle = FirstLetterToUpper(fields["categoryFolder"].ToString()) + " - ❤ " + fields["karma"] + " - Downloads " + fields["downloads"] + " - Compatible versions: " + fields["compatVersions"] + Environment.NewLine + Truncate(fields["body"].ToString(), 140);
                        break;
                    case "documentation":
                        subtitle = Truncate(fields["body"].ToString(), 140);
                        break;
                    default:
                        subtitle = fields["url"].ToString();
                        break;
                }
                return subtitle;
            }
            catch (ExternalException e)
            {
                return e.ToString();
            }
        }

        public string GetIcon(JToken fields)
        {
            switch (fields["nodeTypeAlias"].ToString())
            {
                case "forum":
                    return "our.forum" + (fields["replies"].ToString() != "0" ? ".replies" : "") + ".png";
                case "project":
                    return "our.package.png";
                case "documentation":
                    return "our.docs.png";
                default:
                    return "our.png";
            }
        }

        public List<Result> Query(Query query)
        {

            WebClient client = new WebClient();

            var search = query.Search;
            var method = "GetGlobalSearchResults";
            var fullSearchPostfix = "";
            var fullSearchTitle = "";

            if (query.FirstSearch == "/p")
            {
                method = "GetProjectSearchResults";
                fullSearchPostfix = "&cat=project";
                fullSearchTitle = "projects";
                search = query.SecondToEndSearch;
            }

            if (query.FirstSearch == "/d")
            {
                method = "GetDocsSearchResults";
                fullSearchPostfix = "&cat=documentation";
                fullSearchTitle = "documentation";
                search = query.SecondToEndSearch;
            }

            if (query.FirstSearch == "/f")
            {
                method = "GetForumSearchResults";
                fullSearchPostfix = "&cat=forum";
                fullSearchTitle = "forum";
                search = query.SecondToEndSearch;
            }

            if (search != "/" && search != "")
            {
                var rawResponse =
                    client.DownloadString("https://our.umbraco.org/umbraco/api/OurSearch/" + method + "/?term=" + search);
                JObject response = JObject.Parse(rawResponse);
                JArray items = (JArray) response["items"];

                var result = new List<Result>();

                foreach (var item in items.Take(5))
                {
                    result.Add(new Result()
                    {
                        Title = item["Fields"]["nodeName"].ToString(),
                        SubTitle = GenerateSubTitle(item["Fields"]),
                        IcoPath = GetIcon(item["Fields"]),
                        Action = c =>
                        {
                            try
                            {
                                Process.Start("https://our.umbraco.org" + item["Fields"]["url"].ToString());
                                return true;
                            }
                            catch (ExternalException e)
                            {
                                MessageBox.Show("Open failed, please try later");
                                return false;
                            }
                        }
                    });
                }

                result.Add(new Result()
                {
                    Title =
                        "Search " + fullSearchTitle + (fullSearchTitle != "" ? " on " : "") + "our.umbraco.org for '" +
                        search + "'",
                    SubTitle = items.Count + " result" + (items.Count != 1 ? "s" : ""),
                    IcoPath = "our.png",
                    Action = c =>
                    {
                        try
                        {
                            Process.Start("https://our.umbraco.org/search?q=" + search + fullSearchPostfix);
                            return true;
                        }
                        catch (ExternalException e)
                        {
                            MessageBox.Show("Open failed, please try later");
                            return false;
                        }
                    }
                });

                return result;
            }
            else
            {
                return new List<Result>();
            }
        }
    }
}