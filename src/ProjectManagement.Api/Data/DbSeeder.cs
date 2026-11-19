using Microsoft.EntityFrameworkCore;
using ProjectManagement.Api.Models;

namespace ProjectManagement.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Users.AnyAsync())
        {
            await SeedRequestedDevelopersAsync(context);
            return;
        }

        // ===== Users =====
        var users = new List<User>
        {
            new() { Id = 1, DisplayName = "Carlos Mendoza", Email = "carlos.mendoza@empresa.com", Role = "Developer" },
            new() { Id = 2, DisplayName = "Ana García", Email = "ana.garcia@empresa.com", Role = "Developer" },
            new() { Id = 3, DisplayName = "Roberto Sánchez", Email = "roberto.sanchez@empresa.com", Role = "Developer" },
            new() { Id = 4, DisplayName = "María López", Email = "maria.lopez@empresa.com", Role = "Developer" },
            new() { Id = 5, DisplayName = "Diego Torres", Email = "diego.torres@empresa.com", Role = "Developer" },
            new() { Id = 6, DisplayName = "Laura Ramírez", Email = "laura.ramirez@empresa.com", Role = "ProductOwner" },
            new() { Id = 7, DisplayName = "Fernando Castillo", Email = "fernando.castillo@empresa.com", Role = "ProductOwner" },
            new() { Id = 8, DisplayName = "Patricia Herrera", Email = "patricia.herrera@empresa.com", Role = "ProductOwner" },
            new() { Id = 9, DisplayName = "Alejandro Ruiz", Email = "alejandro.ruiz@empresa.com", Role = "ScrumMaster" },
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        // ===== Developers =====
        var developers = new List<Developer>
        {
            new() { Id = 1, UserId = 1, Specialty = "Backend", Seniority = "Senior" },
            new() { Id = 2, UserId = 2, Specialty = "Frontend", Seniority = "Mid" },
            new() { Id = 3, UserId = 3, Specialty = "FullStack", Seniority = "Senior" },
            new() { Id = 4, UserId = 4, Specialty = "QA", Seniority = "Mid" },
            new() { Id = 5, UserId = 5, Specialty = "DevOps", Seniority = "Lead" },
        };
        context.Developers.AddRange(developers);
        await context.SaveChangesAsync();

        // ===== Product Owners =====
        var productOwners = new List<ProductOwner>
        {
            new() { Id = 1, UserId = 6, Department = "Banca Digital" },
            new() { Id = 2, UserId = 7, Department = "Operaciones" },
            new() { Id = 3, UserId = 8, Department = "Riesgo" },
        };
        context.ProductOwners.AddRange(productOwners);
        await context.SaveChangesAsync();

        // ===== Projects =====
        var projects = new List<Project>
        {
            new()
            {
                Id = 1,
                Name = "Portal Banca Digital v2",
                Description = "Rediseño completo del portal de banca digital con nuevas funcionalidades de transferencias y pagos",
                Product = "Banca Digital",
                StartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "Active"
            },
            new()
            {
                Id = 2,
                Name = "API Gateway Microservicios",
                Description = "Implementación del API Gateway para la arquitectura de microservicios corporativa",
                Product = "Plataforma Core",
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "Active"
            },
            new()
            {
                Id = 3,
                Name = "Sistema de Scoring Crediticio",
                Description = "Nuevo motor de scoring crediticio basado en machine learning para evaluación de riesgo",
                Product = "Gestión de Riesgo",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "Planning"
            }
        };
        context.Projects.AddRange(projects);
        await context.SaveChangesAsync();

        // ===== Project Members =====
        var members = new List<ProjectMember>
        {
            // Project 1 - Portal Banca Digital
            new() { ProjectId = 1, UserId = 1, Role = "Developer" },
            new() { ProjectId = 1, UserId = 2, Role = "Developer" },
            new() { ProjectId = 1, UserId = 3, Role = "Developer" },
            new() { ProjectId = 1, UserId = 6, Role = "ProductOwner" },
            new() { ProjectId = 1, UserId = 9, Role = "ScrumMaster" },
            // Project 2 - API Gateway
            new() { ProjectId = 2, UserId = 1, Role = "Developer" },
            new() { ProjectId = 2, UserId = 5, Role = "Developer" },
            new() { ProjectId = 2, UserId = 7, Role = "ProductOwner" },
            new() { ProjectId = 2, UserId = 9, Role = "ScrumMaster" },
            // Project 3 - Scoring Crediticio
            new() { ProjectId = 3, UserId = 3, Role = "Developer" },
            new() { ProjectId = 3, UserId = 4, Role = "Developer" },
            new() { ProjectId = 3, UserId = 8, Role = "ProductOwner" },
        };
        context.ProjectMembers.AddRange(members);
        await context.SaveChangesAsync();

        // ===== Milestones =====
        var milestones = new List<Milestone>
        {
            new() { ProjectId = 1, Title = "MVP Login y Dashboard", DueDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), Status = "Completed", CompletedDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc) },
            new() { ProjectId = 1, Title = "Módulo de Transferencias", DueDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress" },
            new() { ProjectId = 1, Title = "Módulo de Pagos de Servicios", DueDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
            new() { ProjectId = 2, Title = "POC API Gateway", DueDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), Status = "Completed", CompletedDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc) },
            new() { ProjectId = 2, Title = "Integración con servicios legacy", DueDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), Status = "InProgress" },
            new() { ProjectId = 3, Title = "Análisis de datos y modelo inicial", DueDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
        };
        context.Milestones.AddRange(milestones);
        await context.SaveChangesAsync();

        // ===== Commitments =====
        var commitments = new List<Commitment>
        {
            // Project 1 commitments
            new()
            {
                Id = 1, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "API de autenticación OAuth 2.0",
                Description = "Implementar endpoints de login, refresh token y logout con OAuth 2.0",
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                CompletedDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
                Status = "Completed", Priority = "High",
                QADate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 2, ProjectId = 1, DeveloperId = 2, ProductOwnerId = 1,
                Title = "Dashboard principal con gráficos",
                Description = "Desarrollar el dashboard principal con gráficos de resumen de cuentas y movimientos",
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
                CompletedDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                Status = "Completed", Priority = "High",
                QADate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 3, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Módulo de transferencias entre cuentas propias",
                Description = "Implementar la funcionalidad de transferencias entre cuentas del mismo cliente",
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High",
                QADate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 4, ProjectId = 1, DeveloperId = 3, ProductOwnerId = 1,
                Title = "Transferencias a terceros con validación",
                Description = "Implementar transferencias a terceros con validación de beneficiarios y límites",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "Critical",
                QADate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 5, ProjectId = 1, DeveloperId = 2, ProductOwnerId = 1,
                Title = "Interfaz de pago de servicios básicos",
                Description = "Desarrollar la UI para pago de agua, luz, teléfono y gas",
                StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "Medium",
                QADate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            },

            // Project 2 commitments
            new()
            {
                Id = 6, ProjectId = 2, DeveloperId = 1, ProductOwnerId = 2,
                Title = "Configuración de Kong API Gateway",
                Description = "Instalar y configurar Kong como API Gateway con rate limiting y autenticación",
                StartDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                CompletedDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = "Completed", Priority = "Critical",
                QADate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Id = 7, ProjectId = 2, DeveloperId = 5, ProductOwnerId = 2,
                Title = "Pipeline CI/CD para microservicios",
                Description = "Crear pipelines de CI/CD en Azure DevOps para despliegue automático de microservicios",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High",
                QADate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                Notes = "Pendiente definir estrategia de rollback"
            },
            new()
            {
                Id = 8, ProjectId = 2, DeveloperId = 1, ProductOwnerId = 2,
                Title = "Integración con sistema de cuentas legacy",
                Description = "Crear adaptadores para conectar el API Gateway con los servicios SOAP del sistema de cuentas",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "High",
                QADate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), // Same QA date as commitment 3 → delivery conflict
                PREDate = new DateTime(2026, 8, 22, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            },
            // Additional commitment for Carlos Mendoza to trigger overload (3 active commitments)
            new()
            {
                Id = 11, ProjectId = 1, DeveloperId = 1, ProductOwnerId = 1,
                Title = "Notificaciones push en tiempo real",
                Description = "Implementar sistema de notificaciones push con SignalR para alertas de transacciones",
                StartDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "Medium",
                QADate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            },

            // Project 3 commitments
            new()
            {
                Id = 9, ProjectId = 3, DeveloperId = 3, ProductOwnerId = 3,
                Title = "Modelo de datos de scoring",
                Description = "Diseñar y crear el modelo de datos para el motor de scoring crediticio",
                StartDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                Status = "InProgress", Priority = "High",
            },
            new()
            {
                Id = 10, ProjectId = 3, DeveloperId = 4, ProductOwnerId = 3,
                Title = "Plan de pruebas de scoring",
                Description = "Elaborar plan de pruebas integral para validación del motor de scoring con datos históricos",
                StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                CommitmentDate = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending", Priority = "Medium",
                QADate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        context.Commitments.AddRange(commitments);
        await context.SaveChangesAsync();

        // ===== Deliverables =====
        var deliverables = new List<Deliverable>
        {
            new()
            {
                ProjectId = 1, CommitmentId = 1,
                Title = "API Auth - Endpoints OAuth 2.0",
                Status = "Delivered",
                QADate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                ProjectId = 1, CommitmentId = 2,
                Title = "Dashboard - Componentes React",
                Status = "Delivered",
                QADate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                ProjectId = 1, CommitmentId = 3,
                Title = "API Transferencias propias - Backend",
                Status = "InProgress",
                QADate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                ProjectId = 2, CommitmentId = 6,
                Title = "Kong Gateway - Configuración base",
                Status = "Delivered",
                QADate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                PREDate = new DateTime(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc),
                PROTDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        context.Deliverables.AddRange(deliverables);
        await context.SaveChangesAsync();

        // ===== Environment Deliveries =====
        var envDeliveries = new List<EnvironmentDelivery>
        {
            // Commitment 1 - Auth API (completed all envs)
            new() { CommitmentId = 1, Environment = "QA", PlannedDate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), ActualDate = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), Status = "Deployed" },
            new() { CommitmentId = 1, Environment = "PRE", PlannedDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), ActualDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), Status = "Deployed" },
            new() { CommitmentId = 1, Environment = "PROT", PlannedDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), ActualDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), Status = "Deployed" },
            // Commitment 3 - Transferencias propias (in progress)
            new() { CommitmentId = 3, Environment = "QA", PlannedDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
            new() { CommitmentId = 3, Environment = "PRE", PlannedDate = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
            new() { CommitmentId = 3, Environment = "PROT", PlannedDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
            // Commitment 7 - Pipeline CI/CD (QA deployed)
            new() { CommitmentId = 7, Environment = "QA", PlannedDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), ActualDate = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc), Status = "Deployed" },
            new() { CommitmentId = 7, Environment = "PRE", PlannedDate = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
            new() { CommitmentId = 7, Environment = "PROT", PlannedDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), Status = "Pending" },
        };
        context.EnvironmentDeliveries.AddRange(envDeliveries);
        await context.SaveChangesAsync();

        // ===== Audit Logs =====
        var auditLogs = new List<AuditLog>
        {
            new() { UserId = 9, Action = "Create", EntityType = "Project", EntityId = 1, NewValues = "{\"Name\":\"Portal Banca Digital v2\"}" },
            new() { UserId = 9, Action = "Create", EntityType = "Project", EntityId = 2, NewValues = "{\"Name\":\"API Gateway Microservicios\"}" },
            new() { UserId = 9, Action = "Create", EntityType = "Project", EntityId = 3, NewValues = "{\"Name\":\"Sistema de Scoring Crediticio\"}" },
            new() { UserId = 1, Action = "Update", EntityType = "Commitment", EntityId = 1, OldValues = "{\"Status\":\"InProgress\"}", NewValues = "{\"Status\":\"Completed\"}" },
        };
        context.AuditLogs.AddRange(auditLogs);
        await context.SaveChangesAsync();

        // ===== Run conflict detection =====
        var conflictService = new ProjectManagement.Api.Services.ConflictDetectionService(context);
        await conflictService.DetectConflictsAsync();
        await SeedRequestedDevelopersAsync(context);
    }

    private static async Task SeedRequestedDevelopersAsync(AppDbContext context)
    {
        var names = new[]
        {
            "Lozano Velázquez Oscar Javier",
            "Pacheco Luna Ana Karina",
            "Honorato Mejía Alberto",
            "Martínez Delgado Roberto Valentín",
            "Velázquez López Juan Carlos",
            "Dorantes Martínez Alejandra",
            "Peña Pacheco Lisbet"
        };
        var users = await context.Users.ToListAsync();
        var developers = await context.Developers
            .Select(developer => developer.User.DisplayName)
            .ToListAsync();
        var knownDevelopers = developers
            .Select(NormalizeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (knownDevelopers.Contains(NormalizeName(name)))
                continue;

            var user = users.FirstOrDefault(item =>
                item.Role == "Developer"
                && NormalizeName(item.DisplayName).Equals(NormalizeName(name), StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                user = new User
                {
                    DisplayName = name,
                    Email = null,
                    Role = "Developer",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
                users.Add(user);
            }

            if (user.Id == 0)
                await context.SaveChangesAsync();

            context.Developers.Add(new Developer
            {
                UserId = user.Id,
                Specialty = string.Empty,
                Seniority = string.Empty,
                CreatedAt = DateTime.UtcNow
            });
            knownDevelopers.Add(NormalizeName(name));
        }

        await context.SaveChangesAsync();
    }

    private static string NormalizeName(string value) =>
        value.Trim().Normalize(System.Text.NormalizationForm.FormKC);
}
