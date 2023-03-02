﻿using dotNS;
using Henson.ViewModels;
using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Henson.Models
{
    public class NsClient
    {
        public DotNS APIClient { get; } = new();
        public RestClient HttpClient = new("https://www.nationstates.net");
        public string UserAgent
        {
            get => APIClient.UserAgent;
            set
            {
                APIClient.UserAgent = Uri.EscapeDataString($"Henson v{GetType().Assembly.GetName().Version} developed by nation: Notanam in use by nation: {value}");
            }
        }
        private static string UserClick => DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

        private const int MultipleRequestsWaitTime = 750;

        public (List<Nation> nations, bool authFailedOnSome) AuthenticateAndReturnInfo(List<NationLoginViewModel> logins)
        {
            List<Nation> retVal = new();
            bool didAuthFail = false;
            foreach(NationLoginViewModel login in logins)
            {
                NameValueCollection nvc = new()
                {
                    { "nation", login.Name },
                    { "q", "ping+name+flag+region" }
                };

                try
                {
                    var response = Utilities.API(nvc, login.Pass, 0, UserAgent); //why is this a thing that even throws an exception???
                    XmlNodeList xmlResp = Utilities.Parse(Utilities.StrResp(response));

                    var region = char.ToUpper(xmlResp.FindProperty("region")[0]) + xmlResp.FindProperty("region")[1..];
                    retVal.Add(new Nation(xmlResp.FindProperty("name"), login.Pass, xmlResp.FindProperty("flag"), region));
                }
                catch (Exception) { didAuthFail = true; }
                Thread.Sleep(MultipleRequestsWaitTime); //avoid ratelimits
            }

            return (retVal, didAuthFail);
        }

        public bool Ping(NationLoginViewModel login)
        {
            NameValueCollection nvc = new()
            {
                { "nation", login.Name },
                { "q", "ping" }
            };

            try
            {
                Utilities.API(nvc, login.Pass, 0, UserAgent);
            }
            catch (Exception) { return false; }
            return true;
        }

        public List<bool> PingMany(List<NationLoginViewModel> logins)
        {
            List<bool> loginSuccesses = new();
            foreach(var n in logins)
            {
                loginSuccesses.Add(Ping(n));
                Thread.Sleep(MultipleRequestsWaitTime);
            }

            return loginSuccesses;
        }

        public string? FindWA(List<NationGridViewModel> nations)
        {
            NameValueCollection nvc = new()
            {
                { "wa", "1" },
                { "q", "members" }
            };

            var response = Utilities.API(nvc, null, 0, UserAgent);

            XmlNodeList nodelist = Utilities.Parse(Utilities.StrResp(response), "*");

            HashSet<string> members = nodelist.FindProperty("MEMBERS").Replace('_', ' ').Split(',').ToHashSet();

            foreach(var n in nations)
            {
                if(members.Contains(n.Name.ToLower()))
                {
                    return n.Name;
                }
            }

            return null;
        }

        public string? Login(NationLoginViewModel login)
        {
            var request = new RestRequest("/template-overall=none/page=un", Method.Get);
            request.AddHeader("User-Agent", UserAgent);
            request.AddParameter("nation", login.Name);
            request.AddParameter("password", login.Pass);
            request.AddParameter("logging_in", "1");
            request.AddParameter("userclick", UserClick);

            var response = HttpClient.Execute(request);

            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.Content);

                var chk = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='chk']").Attributes["value"].Value;

                return chk;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool ApplyWA(string chk)
        {
            var request = new RestRequest("/template-overall=none/page=UN_status", Method.Post);
            request.AddHeader("User-Agent", UserAgent);
            request.AddParameter("action", "join_UN");
            request.AddParameter("chk", chk);
            request.AddParameter("submit", "1");
            request.AddParameter("userclick", UserClick);

            var response = HttpClient.Execute(request);

            return response.Content != null && response.Content.Contains("has been received!");
        }

        public string? GetLocalID()
        {
            var request = new RestRequest("/template-overall=none/page=settings", Method.Get);
            request.AddHeader("User-Agent", UserAgent);

            var response = HttpClient.Execute(request);

            if (response.IsSuccessStatusCode)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(response.Content);

                var localID = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='localid']").Attributes["value"].Value;

                return localID;
            }

            return null;
        }

        public bool MoveToJP(string targetRegion, string localID)
        {
            var request = new RestRequest("/template-overall=none/page=change_region", Method.Post);
            request.AddHeader("User-Agent", UserAgent);
            request.AddParameter("localid", localID);
            request.AddParameter("region_name", targetRegion);
            request.AddParameter("move_region", "1");
            request.AddParameter("userclick", UserClick);

            var response = HttpClient.Execute(request);

            return response.Content != null && response.Content.Contains("Success!");
        }
    }
}
