using StarHotel.Domain.Enums;

namespace StarHotel.Api.Services;

/// <summary>
/// Pricing module — pure calculation logic, stateless (BR-18–BR-23)
/// </summary>
public class PricingService
{
    private const decimal DefaultDeposit = 20m;
    private static readonly TimeSpan CheckoutCutoff = new(14, 0, 0); // 2:00 PM

    /// <summary>
    /// BR-18: SubTotal = StayDuration × RoomPrice
    /// </summary>
    public decimal CalculateSubTotal(int stayDuration, decimal roomPrice) =>
        stayDuration * roomPrice;

    /// <summary>
    /// BR-19: TotalDue = SubTotal + Deposit
    /// </summary>
    public decimal CalculateTotalDue(decimal subTotal, decimal deposit) =>
        subTotal + deposit;

    /// <summary>
    /// BR-23: Check-out date calculation based on check-in time
    /// If check-in >= 12:00 PM: checkout = checkin + duration days
    /// If check-in < 12:00 PM: checkout = checkin + (duration - 1) days
    /// Always at 12:00 PM
    /// </summary>
    public DateTime CalculateCheckOutDate(DateTime checkIn, int stayDuration)
    {
        var checkInTime = checkIn.TimeOfDay;
        var noonCutoff = new TimeSpan(12, 0, 0);

        var checkOutDate = checkInTime >= noonCutoff
            ? checkIn.Date.AddDays(stayDuration)
            : checkIn.Date.AddDays(stayDuration - 1);

        return checkOutDate.AddHours(12); // Always 12:00 PM
    }

    /// <summary>
    /// BR-14: After 2:00 PM checkout, deposit refund is 0 ("DEPOSIT NO REFUND")
    /// </summary>
    public decimal CalculateRefund(DateTime checkOutTime, decimal deposit) =>
        checkOutTime.TimeOfDay >= CheckoutCutoff ? 0m : deposit;

    /// <summary>
    /// BR-21: Temporary receipt SubTotal = Payment - Deposit
    /// </summary>
    public decimal TemporaryReceiptSubTotal(decimal payment, decimal deposit) =>
        payment - deposit;

    /// <summary>
    /// BR-22: Official receipt Total = Payment - Refund
    /// </summary>
    public decimal OfficialReceiptTotal(decimal payment, decimal refund) =>
        payment - refund;

    /// <summary>
    /// BR-20: Default deposit is 20.00
    /// </summary>
    public decimal DefaultDepositAmount() => DefaultDeposit;
}