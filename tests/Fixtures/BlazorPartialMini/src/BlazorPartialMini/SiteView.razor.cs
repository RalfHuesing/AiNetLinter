using Microsoft.AspNetCore.Components;

namespace BlazorPartialMini;

public partial class SiteView
{
    [Parameter]
    public string? Message { get; set; }

    protected override Task OnInitializedAsync() => base.OnInitializedAsync();

    protected override void OnParametersSet() => base.OnParametersSet();
}
