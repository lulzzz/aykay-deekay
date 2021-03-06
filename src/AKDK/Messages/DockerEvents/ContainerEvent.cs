using Newtonsoft.Json;
using System;

namespace AKDK.Messages.DockerEvents
{
	/// <summary>
    /// 	Represents an event relating to a container. 
    /// </summary>
	public class ContainerEvent
		: DockerEvent
	{
		/// <summary>
        ///		Initialise the <see cref="ContainerEvent"/>. 
        /// </summary>
		public ContainerEvent(DockerEventType eventType)
            : base(DockerEventTarget.Container, eventType)
		{
		}

        /// <summary>
        ///     The Id of the container that the event relates to.
        /// </summary>
        [JsonProperty("id")]
        public string ContainerId { get; set; }

		/// <summary>
		/// 	The name of the container that the event relates to.
		/// </summary>
		[JsonIgnore]
		public string Name => GetActorAttribute("name");

		/// <summary>
		/// 	The image used to the container that the event relates to.
		/// </summary>
		[JsonIgnore]
		public string Image => GetActorAttribute("image");
	}

	/// <summary>
    ///		Event raised when a container is created. 
    /// </summary>
	public class ContainerCreated
		: ContainerEvent
	{
		/// <summary>
        ///		Create a new <see cref="ContainerCreated"/> event model. 
        /// </summary>
		public ContainerCreated()
            : base(DockerEventType.Create)
		{
		}
	}

    /// <summary>
    ///		Event raised when a container is started. 
    /// </summary>
	public class ContainerStarted
        : ContainerEvent
    {
        /// <summary>
        ///		Create a new <see cref="ContainerStarted"/> event model. 
        /// </summary>
        public ContainerStarted()
            : base(DockerEventType.Start)
        {
        }
    }

    /// <summary>
    ///		Model for the event raised when a container has been terminated. 
    /// </summary>
    public class ContainerDied
		: ContainerEvent
	{
		/// <summary>
        ///		Create a new <see cref="ContainerDied"/> event model. 
        /// </summary>
		public ContainerDied()
            : base(DockerEventType.Die)
		{
		}

        /// <summary>
        ///     The container exit code.
        /// </summary>
        public int ExitCode => Int32.Parse(
            GetActorAttribute("exitCode")
        );
	}

    /// <summary>
    ///		Model for the event raised when a container has been destroyed. 
    /// </summary>
	public class ContainerDestroyed
        : ContainerEvent
    {
        /// <summary>
        ///		Create a new <see cref="ContainerDestroyed"/> event model. 
        /// </summary>
        public ContainerDestroyed()
            : base(DockerEventType.Destroy)
        {
        }
    }
}
