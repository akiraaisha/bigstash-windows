﻿using System;
using System.Runtime.Serialization;

namespace DeepfreezeModel
{
    [DataContract]
    public class S3Info
    {
        [DataMember(Name="bucket")]
        public string Bucket { get; set; }

        [DataMember(Name="prefix")]
        public string Prefix { get; set; }

        [DataMember(Name="token_expiration")]
        public DateTime TokenExpiration { get; set; }

        [DataMember(Name="token_session")]
        public string TokenSession { get; set; }

        [DataMember(Name="token_uid")]
        public string TokenUID { get; set; }

        [DataMember(Name="token_secret_key")]
        public string TokenSecretKey { get; set; }

        [DataMember(Name="token_access_key")]
        public string TokenAccessKey { get; set; }
    }
}
