# BMad Learning Project

# A: Clarify

## Problem Statement:
Make a web application to show your understanding of the BMad method and the DORA metrics. In this case I will make a web application for users to schedule haircut appointments at a barbershop.

## Acceptance Criteria
There is a well-designed, interactive Homepage
Proper user authentication with different permission levels
Customers can select barbers and schedule appointments
Appointments are stored in the database
Barber accounts have permission to view their appointments and cancel said appointments
Admin accounts have permission to view appointments, cancel them, and manage accounts, including creating barbers, and changing passwords

## Confirmed Facts
The project will use .NET, JS, React, SQLite.
I will use the BMad method and autotesting.

## Assumptions
Not publicly hosting

## Questions
How do I want the UI to look?
Should I implement canceling appointments for customers?
Do we need an entire admin page or just a button?
Does .NET have built in authentication I can use?
How many stories will be BMad need?

## Out of scope behavior
No need for an actual calender
No text message or email reminders
Probably don't need forgot password

# B: Plan
No changes in repository since it is a new project

Authentication should validate accounts, with hashed passwords, any error should show an error message to the user that his or her username or password is wrong
Times that are already booked should not be visible to the user, but in the case they are validate and error handle
Appointments must validate that a date is valid, is not in the past, and is not double booked
Errors should stop any appointment from being made and put a simple error message on the website for the user to be notified
Users should only be allowed to view their appointments, with the exception of admins. Names should be shown in appointments, and we will take their email. Other than that no personal information should be collected


