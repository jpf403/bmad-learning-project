# BMad Learning Project

## Fake Barbershop Appointment Maker

This is a web application for users to make appointments for a haircut at a fake barbershop. This is a project for learning how to use the BMad method and following DORA principles.

## Run This Project

### Backend
bmad-learning-project/backend/BarbershopApi/ \
dotnet run --launch-profile https

### Frontend
bmad-learning-project/frontend/
npm run dev

### Create launchSettings.json

In bmad-learning-project/backend/BarbershopApi/Properties, use the launchSettings.example.json as a template. Then fill in the JWT__Key, Admin__Email, and Admin__Password fields for https and http.

## Run tests

All tests run automatically on push. You can run them manually with the commands below.

### Backend

bmad-learning-project/backend/BarbershopApi/ \
dotnet test

### Frontend

bmad-learning-project/frontend/
npm test \
npm run lint \
npm run format:check

