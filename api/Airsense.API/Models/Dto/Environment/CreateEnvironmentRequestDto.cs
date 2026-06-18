using System.ComponentModel.DataAnnotations;

namespace Airsense.API.Models.Dto.Environment;

public class CreateEnvironmentRequestDto
{
    [Length(3,20)]
    public string Name { get; set; }

    [Length(1,64)]
    public string Icon { get; set; } = "building";
}
