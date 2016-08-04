using System;

namespace MonoDevelop.ConnectedServices
{
	/// <summary>
	/// Defines the commands for Connected Services
	/// </summary>
	public enum Commands
	{
		/// <summary>
		/// Opens the services gallery tab
		/// </summary>
		OpenServicesGallery,
		OpenServicesGalleryFromServicesNode,

		/// <summary>
		/// Opens the service details tab for the given service
		/// </summary>
		OpenServiceDetails,
	}
}
