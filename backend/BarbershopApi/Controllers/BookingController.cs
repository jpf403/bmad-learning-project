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

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] string? date, [FromQuery] int? barberId)
    {
        var account = (Account)HttpContext.Items["Account"]!;

        int targetBarberId;
        if (account.Role == Role.Barber)
        {
            targetBarberId = account.Id;
        }
        else if (account.Role == Role.Admin)
        {
            if (barberId is null)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "barberId is required.");
            }
            var barber = await accountRepository.FindById(barberId.Value);
            if (barber is null || barber.Role != Role.Barber)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Selected barber is not available.");
            }
            targetBarberId = barberId.Value;
        }
        else
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only barbers and admins can view a schedule.");
        }

        // Model binding maps both an omitted `date` and a present-but-empty `date=` to the
        // same null value, so `date is not null` can't tell "no date supplied" (use today)
        // apart from "date supplied empty" (malformed, should 400) -- checking the raw query
        // string directly is what actually distinguishes the two.
        var dateWasSupplied = Request.Query.ContainsKey("date");
        if (dateWasSupplied && !ValidCalendarDateAttribute.IsValidDate(date ?? string.Empty))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Date must be in yyyy-MM-dd format.");
        }
        return Ok(await bookingService.GetDaySchedule(targetBarberId, dateWasSupplied ? date : null));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyAppointments()
    {
        var account = (Account)HttpContext.Items["Account"]!;
        return Ok(await bookingService.FindUpcomingByCustomer(account.Id));
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var account = (Account)HttpContext.Items["Account"]!;
        try
        {
            await bookingService.Cancel(id, account.Id, account.Role);
            return NoContent();
        }
        catch (AppointmentNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Appointment not found.");
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This appointment has already been cancelled.");
        }
        catch (AppointmentAlreadyFinishedException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This appointment has already finished and cannot be cancelled.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }
}
