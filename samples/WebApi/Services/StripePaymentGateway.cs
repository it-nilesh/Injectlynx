namespace WebApi.Services;

public sealed class StripePaymentGateway : IPaymentGateway
{
    public string GetName() => "stripe";
}
