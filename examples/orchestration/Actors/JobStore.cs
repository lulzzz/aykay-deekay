﻿using Akka.Actor;
using AKDK.Actors;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;

namespace AKDK.Examples.Orchestration.Actors
{
    using Messages;

    /// <summary>
    ///     Actor used to persist information about active jobs.
    /// </summary>
    public class JobStore
        : ReceiveActorEx
    {
        /// <summary>
        ///     The default name for instances of the <see cref="JobStoreEvents"/> actor.
        /// </summary>
        public static readonly string ActorName = "job-store";

        /// <summary>
        ///     Serialiser settings for persisting job store data.
        /// </summary>
        static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter()
            }
        };

        /// <summary>
        ///     The file used to persist job store data.
        /// </summary>
        readonly FileInfo       _storeFile;

        /// <summary>
        ///     The serialiser used to persist store data.
        /// </summary>
        readonly JsonSerializer _serializer = JsonSerializer.Create(SerializerSettings);

        /// <summary>
        ///     The current job store data.
        /// </summary>
        JobStoreData            _data;

        /// <summary>
        ///     A reference to the actor that manages the job-store event bus.
        /// </summary>
        IActorRef               _jobStoreEvents;

        /// <summary>
        ///     Create a new <see cref="JobStore"/> actor.
        /// </summary>
        /// <param name="storeFile">
        ///     The name of the file used to persist job store data.
        /// </param>
        public JobStore(string storeFile)
        {
            if (String.IsNullOrWhiteSpace(storeFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(storeFile)}.", nameof(storeFile));

            _storeFile = new FileInfo(storeFile);
        }

        /// <summary>
        ///     Called when the actor is ready to handle messages.
        /// </summary>
        void Ready()
        {
            Receive<CreateJob>(createJob =>
            {
                JobData newJob = new JobData
                {
                    Id = _data.NextJobId++,
                    TargetUrl = createJob.TargetUrl
                };
                _data.Jobs.Add(newJob.Id, newJob);

                Persist();

                _jobStoreEvents.Tell(new JobCreated(
                    correlationId: createJob.CorrelationId,
                    jobId: newJob.Id,
                    targetUrl: newJob.TargetUrl
                ));
            });
            Forward<EventBusActor.Subscribe>(_jobStoreEvents);
            Forward<EventBusActor.Unsubscribe>(_jobStoreEvents);
        }

        /// <summary>
        ///     Called when the actor is started.
        /// </summary>
        protected override void PreStart()
        {
            base.PreStart();

            InitializeStore();
            _jobStoreEvents = Context.ActorOf(
                JobStoreEvents.Create(),
                name: "events"
            );
            Context.Watch(_jobStoreEvents);

            Become(Ready);
        }

        /// <summary>
        ///     Initialise the job store.
        /// </summary>
        void InitializeStore()
        {
            if (_storeFile.Exists)
            {
                using (StreamReader storeReader = _storeFile.OpenText())
                using (JsonTextReader jsonReader = new JsonTextReader(storeReader))
                {
                    _data = _serializer.Deserialize<JobStoreData>(jsonReader);
                }
            }
            else
            {
                _data = new JobStoreData
                {
                    NextJobId = 1
                };
                Persist();
            }
        }

        /// <summary>
        ///     Generate <see cref="Props"/> to create a new <see cref="JobStore"/> actor.
        /// </summary>
        /// <param name="storeFile">
        ///     The name of the file used to persist job store data.
        /// </param>
        public static Props Create(string storeFile)
        {
            if (String.IsNullOrWhiteSpace(storeFile))
                throw new ArgumentException($"Argument cannot be null, empty, or entirely composed of whitespace: {nameof(storeFile)}.", nameof(storeFile));

            return Props.Create(
                () => new JobStore(storeFile)
            );
        }

        /// <summary>
        ///     Write job store data to the store file.
        /// </summary>
        void Persist()
        {
            using (StreamWriter storeWriter = _storeFile.CreateText())
            using (JsonTextWriter jsonWriter = new JsonTextWriter(storeWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                _serializer.Serialize(jsonWriter, _data);
            }
        }

        /// <summary>
        ///     Persistence contract for job data.
        /// </summary>
        class JobData
        {
            /// <summary>
            ///     The job Id.
            /// </summary>
            [JsonProperty("id")]
            public int Id { get; set; }

            /// <summary>
            ///     The URL to fetch.
            /// </summary>
            [JsonProperty("targetUrl")]
            public Uri TargetUrl { get; set; }
        }

        /// <summary>
        ///     Persistence contract for job data.
        /// </summary>
        class JobStoreData
        {
            /// <summary>
            ///     The next available job Id.
            /// </summary>
            [JsonProperty("nextJobId")]
            public int NextJobId { get; set; }

            /// <summary>
            ///     All jobs known to the job store, keyed by job Id.
            /// </summary>
            [JsonProperty("jobs", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
            public Dictionary<int, JobData> Jobs { get; } = new Dictionary<int, JobData>();
        }
    }
}