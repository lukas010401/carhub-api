namespace CarHub.Api.Application.Contracts.Media;

public sealed class ReorderListingImagesRequest
{
    public List<Guid> ImageIds { get; set; } = [];
}
