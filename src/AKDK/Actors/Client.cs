﻿using Akka.Actor;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AKDK.Actors
{
    using Messages;

    /// <summary>
    ///     Actor that aggregates a <see cref="Connection"/>, providing a public surface for the Docker API.
    /// </summary>
    public class Client
        : ReceiveActorEx
    {
        /// <summary>
        ///     Regular expression that matches the Docker log line prefix.
        /// </summary>
        /// <remarks>
        ///     TODO: Replace use of <see cref="Streaming.StreamLines"/> actor with an actor that understands the Docker log-header format (and associated framing logic).
        /// </remarks>
        /// <seealso cref="DockerLogEntry"/>
        static readonly Regex MatchLogPrefix = new Regex("[\x00\x01\x02]\x00{6}.{1}");

        /// <summary>
        ///     <see cref="Props"/> that can be used to create the <see cref="Connection"/> actor used to execute <see cref="Connection.Command"/>s.
        /// </summary>
        readonly Props  _connectionProps;

        /// <summary>
        ///     A reference to the <see cref="Connection"/> actor used to execute <see cref="Connection.Command"/>s.
        /// </summary>
        IActorRef       _connection;

        /// <summary>
        ///     Create a new <see cref="Client"/> actor.
        /// </summary>
        /// <param name="connectionProps">
        ///     <see cref="Props"/> that can be used to create the <see cref="Connection"/> actor used to execute <see cref="Connection.Command"/>s.
        /// </param>
        public Client(Props connectionProps)
        {
            if (connectionProps == null)
                throw new ArgumentNullException(nameof(connectionProps));

            _connectionProps = connectionProps;
        }

        /// <summary>
        ///     Create a new <see cref="Client"/> actor.
        /// </summary>
        /// <param name="connection">
        ///     A reference to the <see cref="Connection"/> actor used to execute <see cref="Connection.Command"/>s.
        /// </param>
        /// <remarks>
        ///     Used for testing purposes (e.g. inject TestProbe).
        /// </remarks>
        public Client(IActorRef connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            _connection = connection;
        }

        /// <summary>
        ///     Called when the <see cref="Client"/> is ready to handle requests.
        /// </summary>
        void Ready()
        {
            // TODO: Handle termination of underlying Connection actor.

            Receive<ListImages>(listImages =>
            {
                Log.Debug("Received ListImages request '{0}' from '{1}'.", listImages.CorrelationId, Sender);

                var executeCommand = new Connection.ExecuteCommand(listImages, async (dockerClient, cancellationToken) =>
                {
                    IList<ImagesListResponse> images = await dockerClient.Images.ListImagesAsync(listImages.Parameters);

                    return new ImageList(listImages.CorrelationId, images);
                });

                _connection.Tell(executeCommand, Sender);
            });
            Receive<CreateContainer>(createContainer =>
            {
                Log.Debug("Received CreateContainer request '{0}' from '{1}'.", createContainer.CorrelationId, Sender);

                var executeCommand = new Connection.ExecuteCommand(createContainer, async (dockerClient, cancellationToken) =>
                {
                    CreateContainerResponse response = await dockerClient.Containers.CreateContainerAsync(createContainer.Parameters);

                    return new ContainerCreated(createContainer.CorrelationId, response);
                });

                _connection.Tell(executeCommand, Sender);
            });
            Receive<StartContainer>(startContainer =>
            {
                Log.Debug("Received StartContainer request '{0}' from '{1}'.", startContainer.CorrelationId, Sender);

                var executeCommand = new Connection.ExecuteCommand(startContainer, async (dockerClient, cancellationToken) =>
                {
                    bool containerWasStarted = await dockerClient.Containers.StartContainerAsync(startContainer.ContainerId, startContainer.Parameters);

                    return new ContainerStarted(startContainer.CorrelationId, startContainer.ContainerId,
                        alreadyStarted: !containerWasStarted
                    );
                });

                _connection.Tell(executeCommand, Sender);
            });
            Receive<GetContainerLogs>(getContainerLogs =>
            {
                Log.Debug("Received GetContainerLogs request '{0}' from '{1}'.", getContainerLogs.CorrelationId, Sender);

                // TODO: Rather than use StreamLines, create an actor that reads log entries using the Docker log-entry header / framing (see DockerLogEntry class for details).

                var executeCommand = new Connection.ExecuteCommand(
                    getContainerLogs, async (dockerClient, cancellationToken) =>
                    {
                        Stream responseStream = await dockerClient.Containers.GetContainerLogsAsync(
                            getContainerLogs.ContainerId,
                            getContainerLogs.Parameters,
                            cancellationToken
                        );

                        return new StreamedResponse(getContainerLogs.CorrelationId, responseStream, isLog: true);
                    }
                );

                _connection.Tell(executeCommand, Sender);
            });
        }

        /// <summary>
        ///     Called when the actor is stopped.
        /// </summary>
        protected override void PreStart()
        {
            base.PreStart();

            if (_connection == null)
                _connection = Context.ActorOf(_connectionProps, name: "connection");

            Context.Watch(_connection);

            Become(Ready);
        }

        /// <summary>
        ///     Strip the Docker log line prefix.
        /// </summary>
        /// <param name="logLine">
        ///     The docker log line.
        /// </param>
        /// <returns>
        ///     The line without the prefix.
        /// </returns>
        static string StripLogPrefix(string logLine)
        {
            return MatchLogPrefix.Replace(logLine, String.Empty);
        }
    }
}