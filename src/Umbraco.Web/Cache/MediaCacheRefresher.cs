﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using System.Linq;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// A cache refresher to ensure media cache is updated
    /// </summary>
    /// <remarks>
    /// This is not intended to be used directly in your code and it should be sealed but due to legacy code we cannot seal it.
    /// </remarks>
    public class MediaCacheRefresher : JsonCacheRefresherBase<MediaCacheRefresher>
    {
        #region Json

        internal class JsonPayload
        {
            public JsonPayload(int id, TreeChangeTypes changeTypes)
            {
                Id = id;
                ChangeTypes = changeTypes;
            }

            public int Id { get; set; }
            public TreeChangeTypes ChangeTypes { get; private set; }
        }

        internal static string Serialize(IEnumerable<JsonPayload> payloads)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(payloads.ToArray());
        }

        internal static JsonPayload[] Deserialize(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<JsonPayload[]>(json);
        }

        #endregion

        #region Define

        protected override MediaCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return new Guid(DistributedCache.MediaCacheRefresherId); }
        }

        public override string Name
        {
            get { return "MediaCacheRefresher"; }
        }
        
        #endregion

        #region Events

        public override void Refresh(string json)
        {
            var payloads = Deserialize(json);
            var svce = PublishedCachesServiceResolver.Current.Service;
            bool anythingChanged;
            svce.NotifyChanges(payloads, out anythingChanged);

            if (anythingChanged)
            {
                ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();

                foreach (var payload in payloads)
                {
                    // note: ClearCacheByKeySearch - does StartsWith(...)

                    // legacy alert!
                    //
                    // library cache library.GetMedia(int mediaId, bool deep) maintains a cache
                    // of media xml - and of *deep* media xml - using the key 
                    // MediaCacheKey + "_" + mediaId + "_" + deep
                    //
                    // this clears the non-deep xml for the current media
                    //
                    ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheByKeySearch(
                        string.Format("{0}_{1}_False", CacheKeys.MediaCacheKey, payload.Id));

                    // and then, for the entire path, we have to clear whatever might contain the media
                    // bearing in mind there are probably nasty race conditions here - this is all legacy
                    var k = string.Format("{0}_{1}_", CacheKeys.MediaCacheKey, payload.Id);
                    var x = ApplicationContext.Current.ApplicationCache.RuntimeCache.GetCacheItem(k)
                        as Tuple<XElement, string>;
                    if (x == null) continue;
                    var path = x.Item2;

                    foreach (var pathId in path.Split(',').Skip(1).Select(int.Parse))
                    {
                        // this clears the deep xml for the medias in the path (skipping -1)
                        ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheByKeySearch(
                            string.Format("{0}_{1}_True", CacheKeys.MediaCacheKey, pathId));
                    }

                    // repository cache
                    // it *was* done for each pathId but really that does not make sense
                    // only need to do it for the current media
                    ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(
                        RepositoryBase.GetCacheIdKey<IMedia>(payload.Id));

                    // remove those that are in the branch
                    if (payload.ChangeTypes.HasTypesAny(TreeChangeTypes.RefreshBranch | TreeChangeTypes.Remove))
                    {
                        var pathid = "," + payload.Id + ",";
                        ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheObjectTypes<IMedia>(
                            (_, v) => v.Path.Contains(pathid));
                    }
                }
            }

            base.Refresh(json);
        }

        // these events should never trigger
        // everything should be JSON

        public override void RefreshAll()
        {
            throw new NotSupportedException();
        }

        public override void Refresh(int id)
        {
            throw new NotSupportedException();
        }

        public override void Refresh(Guid id)
        {
            throw new NotSupportedException();
        }

        public override void Remove(int id)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Indirect

        public void RefreshMediaTypes(IEnumerable<int> refreshedIds, IEnumerable<int> removedIds)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}