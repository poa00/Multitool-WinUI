﻿namespace Multitool.Net.Twitch.Json
{
#pragma warning disable CS0649
#pragma warning disable IDE1006
    internal class JsonEmote
    {
        public string id { get; set; }
        public string name { get; set; }
        public JsonImages images { get; set; }
        public string[] format { get; set; }
        public string[] scale { get; set; }
        public string[] theme_mode { get; set; }
    }

    internal class JsonImages
    {
        public string url_1x { get; set; }
        public string url_2x { get; set; }
        public string url_4x { get; set; }
    }
}