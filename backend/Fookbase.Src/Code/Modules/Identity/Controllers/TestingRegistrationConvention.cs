using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Fookbase.Api.Modules.Identity.Controllers;

// The legacy registration route is only used to create accounts in integration tests.
internal sealed class TestingRegistrationConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        var controller = application.Controllers.Single(model =>
            model.ControllerType.AsType() == typeof(RegistrationController));
        var action = controller.Actions.Single(model =>
            model.ActionMethod.Name == nameof(RegistrationController.RegisterAsync));
        controller.Actions.Remove(action);
    }
}
