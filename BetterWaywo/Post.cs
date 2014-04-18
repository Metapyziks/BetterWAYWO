﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace BetterWaywo
{
    class Post
    {
        private static readonly Regex ContentRegex = new Regex(@"\[(img|vid|media|video)[^\]]*?\]([^\[\]]*?)\[/\1\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private float? _ratingsValue;
        private float? _contentValue;
        private string _message;
        private string _username;

        public readonly int Id;
        public readonly Dictionary<string, int> Ratings;

        [JsonIgnore]
        public float RatingsValue
        {
            get
            {
                if (!_ratingsValue.HasValue)
                    _ratingsValue = Ratings.Sum(r => GetRatingValue(r.Key) * r.Value);
                return _ratingsValue.Value;
            }
        }

        [JsonIgnore]
        public float ContentValue
        {
            get
            {
                if (!_contentValue.HasValue)
                    _contentValue = GetContentValue(Message);
                return _contentValue.Value;
            }
        }

        [JsonIgnore]
        public float ContentMultiplier
        {
            get
            {
                const double height = 0.5;
                const double minimum = 0.5;
                const double preferredValue = 1.5;
                const double standardDeviation = 4.0; // preferredValue +/- standardDeviation = ~60% 

                var res = height * Math.Exp(-(Math.Pow(ContentValue - preferredValue, 2) / (2 * Math.Pow(standardDeviation, 2f)))) + minimum;
                //Console.WriteLine("{0} -> {1:R}", ContentValue, res);

                return (float)res;
            }
        }

        public string Message
        {
            get
            {
                if (_message == null)
                    _message = GetPostContents(Id);
                return _message;
            }
            set
            {
                _message = value;
            }
        }

        [JsonIgnore]
        public string Username
        {
            get
            {
                if (_username == null)
                    _username = Regex.Match(Message, @"\[QUOTE=(.*?);").Groups[1].Value;
                return _username;
            }
        }

        [JsonIgnore]
        public bool HasContent
        {
            get
            {
                return ContentValue > 0;
            }
        }

        public Post(int id, Dictionary<string, int> ratings)
        {
            Id = id;
            Ratings = ratings;

            _ratingsValue = null;
            _contentValue = null;
            _message = null;
            _username = null;
        }

        public bool ShouldSerializeMessage()
        {
            return _message != null;
        }

        public static float GetContentValue(string message)
        {
            float value = 0;

            var contentTags = ContentRegex.Matches(message);
            foreach (var tag in contentTags.Cast<Match>())
            {
                switch (tag.Groups[1].Value.ToLower())
                {
                    case "img":
                    {
                        var isGif = false;

                        Uri uri;
                        if (Uri.TryCreate(tag.Groups[2].Value, UriKind.Absolute, out uri))
                            isGif = Path.GetExtension(uri.LocalPath).ToLower() == ".gif";

                        if (isGif)
                            value += 1.50f;
                        else
                            value += 1.00f;

                        break;
                    }

                    case "vid":
                    case "media":
                    case "video":
                        value += 2.00f;
                        break;
                }
            }

            return value;
        }

        private static float GetRatingValue(string rating)
        {
            switch (rating)
            {
                case "Programming King":
                case "Lua King":
                    return 3.0f;
                case "Winner":
                case "Useful":
                case "Artistic":
                case "Lua Helper":
                    return 2.0f;
                case "Funny":
                case "Informative":
                    return 1.0f;
                default:
                    return -1.0f; // "junk" ratings
            }
        }

        private static string GetPostContents(int postId)
        {
            Console.WriteLine("Reading post {0}", postId);

            using (var request = new WebClient())
            {
                var values = new NameValueCollection();
                values["do"] = "getquotes";
                values["p"] = postId.ToString("D");

                var response = Program.FacepunchEncoding.GetString(request.UploadValues(string.Format("http://facepunch.com/ajax.php?do=getquotes&p={0}", postId), values));
                var lines = response.Split('\n');

                var result = new StringBuilder();
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (i == 0 || i >= lines.Length - 3)
                        continue;

                    if (i == 1)
                        result.Append(line.Substring(17));
                    else
                        result.Append(line);

                    result.Append('\n');
                }

                return result.ToString().Trim();
            }
        }
    }
}
