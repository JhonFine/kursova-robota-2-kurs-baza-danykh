using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManagePayments)]
[Route("api/payments")]
public sealed class PaymentsController(
    RentalDbContext dbContext,
    IPaymentService paymentService) : ApiControllerBase
{
    [HttpGet("rentals/{rentalId:int}")]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetRentalPayments(int rentalId, CancellationToken cancellationToken)
    {
        var payments = await paymentService.GetRentalPaymentsAsync(rentalId, cancellationToken);
        return Ok(payments.Select(ToDto).ToList());
    }

    [HttpGet("rentals/{rentalId:int}/balance")]
    [ProducesResponseType<RentalBalanceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalBalanceDto>> GetRentalBalance(int rentalId, CancellationToken cancellationToken)
    {
        var balance = await paymentService.GetRentalBalanceAsync(rentalId, cancellationToken);
        return Ok(new RentalBalanceDto(rentalId, balance));
    }

    [HttpPost]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Add([FromBody] AddPaymentRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var result = await paymentService.AddPaymentAsync(
            new PaymentRequest(
                request.RentalId,
                employeeId.Value,
                request.Amount,
                request.Method,
                request.Direction,
                request.Notes),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = "Payment creation failed. Check amount and related entities." });
        }

        var payment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == result.PaymentId, cancellationToken);
        if (payment is null)
        {
            return BadRequest(new { message = "Payment created but could not be loaded." });
        }

        return CreatedAtAction(nameof(GetRentalPayments), new { rentalId = payment.RentalId }, ToDto(payment));
    }

    private static PaymentDto ToDto(CarRental.WebApi.Models.Payment item)
        => new(
            item.Id,
            item.RentalId,
            item.EmployeeId,
            item.Amount,
            item.Method,
            item.Direction,
            item.CreatedAtUtc,
            item.Notes);
}
