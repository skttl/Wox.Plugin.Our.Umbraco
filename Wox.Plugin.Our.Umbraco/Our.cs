using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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

        /*public string RelativeDate(DateTime yourDate)
        {
            return "today";
            /*
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
        }*/

        public DateTime StringToDate(string date)
        {
            try
            {
                return DateTime.ParseExact(date, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                return DateTime.Now;
            }
        }

        public string GenerateSubTitle(JToken fields)
        {
            try
            {
                switch (fields["nodeTypeAlias"].ToString())
                {
                    case "forum":
                        var subtitle = "Posted " + StringToDate(fields["createDate"].ToString()) + " by: " +
                                       fields["authorName"];

                        if (fields["replies"].ToString() != "0")
                        {
                            subtitle = subtitle + " - " + fields["replies"] + " replies, last reply " +
                                       StringToDate(fields["updateDate"].ToString()) + " by " +
                                       fields["lastReplyAuthorName"];
                        }
                        else
                        {
                            subtitle = subtitle + " - No replies";
                        }

                        return subtitle;
                    default:
                        return "";
                }
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
                    return "our.forum.png";
                default:
                    return "our.png";
            }
        }

        public List<Result> Query(Query query)
        {

            WebClient client = new WebClient();
            var rawResponse = client.DownloadString("https://our.umbraco.org/umbraco/api/OurSearch/GetGlobalSearchResults/?term=" + query.Search);
            JObject response = JObject.Parse(rawResponse);
            JArray items = (JArray)response["items"];

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
                            Process.Start(item["Fields"]["url"].ToString());
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

            return result;
        }
    }
}