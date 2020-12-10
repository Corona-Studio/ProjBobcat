using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ProjBobcat.Class.Model.LauncherProfile;

namespace ProjBobcat.Class.Model.LauncherAccount
{
    public class AccountModel
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("accessTokenExpiresAt")]
        public DateTime AccessTokenExpiresAt { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("eligibleForMigration")]
        public bool EligibleForMigration { get; set; }

        [JsonProperty("hasMultipleProfiles")]
        public bool HasMultipleProfiles { get; set; }

        [JsonProperty("legacy")]
        public bool Legacy { get; set; }

        [JsonProperty("localId")]
        public string LocalId { get; set; }

        [JsonProperty("minecraftProfile")]
        public AccountProfileModel MinecraftProfile { get; set; }

        [JsonProperty("persistent")]
        public bool Persistent { get; set; }

        [JsonProperty("remoteId")]
        public string RemoteId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("userProperites")]
        public List<AuthPropertyModel> UserProperites { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }
    }
}