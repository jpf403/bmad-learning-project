using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarbershopApi.Controllers;

[ApiController]
[Route("api/booking")]
[Authorize]
public class BookingController(IAccountRepository accountRepository, IBookingService bookingService) : ControllerBase
{
    [HttpGet("barbers")]
    public async Task<IActionResult> GetBarbers()
    {
        var barbers = await accountRepository.FindAllByRole(Role.Barber);
        return Ok(barbers.Select(b => new BarberSummary { Id = b.Id, FirstName = b.FirstName, LastName = b.LastName }));
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] int barberId, [FromQuery] string date)
    {
        if (string.IsNullOrEmpty(date) || !ValidCalendarDateAttribute.IsValidDate(date))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Date must be in yyyy-MM-dd format.");
        }

        var barber = await accountRepository.FindById(barberId);
        if (barber is null || barber.Role != Role.Barber)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Selected barber is not available.");
        }

        return Ok(await bookingService.GetAvailableSlots(barberId, date));
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking(BookAppointmentRequest request)
    {
        var account = (Account)HttpContext.Items["Account"]!;

        var barber = await accountRepository.FindById(request.BarberId);
        if (barber is null || barber.Role != Role.Barber)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Selected barber is not available.");
        }

        try
        {
            var appointment = await bookingService.Create(account.Id, request.BarberId, request.Date, request.StartTime);
            return StatusCode(201, new BookingConfirmation(appointment.Id, $"{barber.FirstName} {barber.LastName}", appointment.Date, appointment.StartTime));
        }
        catch (InvalidBookingWindowException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "That date or time is no longer available for booking.");
        }
        catch (BookingConflictException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "That time is no longer available. Choose another.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }
}
