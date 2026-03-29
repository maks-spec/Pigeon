FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY Frontend/out .
EXPOSE 8080
CMD ["dotnet", "Frontend.dll"]
