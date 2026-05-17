using FairwayHq.Api.Models;

namespace FairwayHq.Api.Data;

public static class Seed
{
    public static void EnsureSeeded(AppDbContext db)
    {
        if (db.Members.Any() || db.Courses.Any() || db.Products.Any())
        {
            return;
        }

        var today = DateTime.UtcNow.Date;
        string D(int offset) => today.AddDays(offset).ToString("yyyy-MM-dd");

        db.Courses.AddRange(
            new Course { Id = "crs_K7nMpQjLxR", Name = "Championship Course", Holes = 18, Par = 72, Yardage = 7124, Rating = 74.2, Slope = 138, Status = "Open", OpenTime = "06:00", CloseTime = "18:00", Notes = "Signature layout with island green on 17." },
            new Course { Id = "crs_R3vBxHfTd2", Name = "Heritage Nine", Holes = 9, Par = 35, Yardage = 3210, Rating = 35.8, Slope = 122, Status = "Open", OpenTime = "07:00", CloseTime = "17:00", Notes = "Walking-only short course, great for juniors." },
            new Course { Id = "crs_J9pYmKwVc8", Name = "Practice Range", Holes = 0, Par = 0, Yardage = 0, Rating = 0, Slope = 0, Status = "Open", OpenTime = "06:00", CloseTime = "20:00", Notes = "Driving range, short-game area, and putting green." }
        );

        db.Members.AddRange(
            new Member { Id = "mbr_J4nKp2vQ8x", FirstName = "Eleanor", LastName = "Park", Email = "eleanor.park@example.com", Phone = "555-0142", Tier = "Full", Handicap = 8.4, JoinDate = "2019-03-12", Active = true, Balance = 0m, Notes = "Prefers early tee times. Allergic to bees — keep epi-pen accessible at starter shed." },
            new Member { Id = "mbr_W7gHk9rTfL", FirstName = "Marcus", LastName = "Reed", Email = "marcus.reed@example.com", Phone = "555-0188", Tier = "Weekday", Handicap = 14.2, JoinDate = "2021-07-01", Active = true, Balance = 125.50m, Notes = "Outstanding F&B from last Friday's scramble — invoice sent." },
            new Member { Id = "mbr_P3xYm5dBqV", FirstName = "Sofia", LastName = "Alvarez", Email = "sofia.alvarez@example.com", Phone = "555-0114", Tier = "Full", Handicap = 3.1, JoinDate = "2015-05-22", Active = true, Balance = 0m },
            new Member { Id = "mbr_F8jRn2KwHt", FirstName = "Daniel", LastName = "O'Connell", Email = "danny.oc@example.com", Phone = "555-0199", Tier = "Corporate", Handicap = 22.6, JoinDate = "2023-01-10", Active = true, Balance = 320m, Notes = "Books corporate guests — send cart staff in advance." },
            new Member { Id = "mbr_C6vDp9LqXz", FirstName = "Priya", LastName = "Shah", Email = "priya.shah@example.com", Phone = "555-0167", Tier = "Social", Handicap = 0, JoinDate = "2022-09-30", Active = false, Status = "Inactive", Balance = 0m }
        );

        db.TeeTimes.AddRange(
            // Historical completed rounds — feed the member overview "recent rounds" / lifetime stats.
            new TeeTime { Id = "tee_H1aLp3KqXr", Date = D(-7), Time = "07:30", CourseId = "crs_K7nMpQjLxR", PlayersJson = "[\"mbr_J4nKp2vQ8x\",\"mbr_P3xYm5dBqV\"]", Cart = true, Status = "Completed", Notes = "" },
            new TeeTime { Id = "tee_H2bMq8RnVw", Date = D(-14), Time = "08:00", CourseId = "crs_K7nMpQjLxR", PlayersJson = "[\"mbr_J4nKp2vQ8x\",\"mbr_W7gHk9rTfL\"]", Cart = false, Status = "Completed", Notes = "Walking" },
            new TeeTime { Id = "tee_H3cNr5SpYq", Date = D(-21), Time = "09:15", CourseId = "crs_R3vBxHfTd2", PlayersJson = "[\"mbr_W7gHk9rTfL\"]", Cart = true, Status = "Completed", Notes = "" },

            // Upcoming bookings
            new TeeTime { Id = "tee_A2nKp7vYjH", Date = D(0), Time = "08:00", CourseId = "crs_K7nMpQjLxR", PlayersJson = "[\"mbr_J4nKp2vQ8x\",\"mbr_P3xYm5dBqV\"]", Cart = true, Status = "Booked", Notes = "" },
            new TeeTime { Id = "tee_X9wQm4LbRf", Date = D(0), Time = "08:15", CourseId = "crs_K7nMpQjLxR", PlayersJson = "[\"mbr_W7gHk9rTfL\"]", Cart = false, Status = "Booked", Notes = "Walking with caddie" },
            new TeeTime { Id = "tee_B5tDx8KgPq", Date = D(1), Time = "10:30", CourseId = "crs_R3vBxHfTd2", PlayersJson = "[\"mbr_F8jRn2KwHt\"]", Cart = true, Status = "Booked", Notes = "Corporate guest, send cart staff" }
        );

        db.Staff.AddRange(
            new StaffMember { Id = "stf_N7kVp3JqMx", FirstName = "Theo", LastName = "Nakamura", Role = "Pro", Email = "theo@fairwayhq.example", Phone = "555-0211", HourlyRate = 65m, Active = true },
            new StaffMember { Id = "stf_Z4hRm9BwTf", FirstName = "Beatrice", LastName = "Long", Role = "Greenkeeper", Email = "bea@fairwayhq.example", Phone = "555-0233", HourlyRate = 32m, Active = true },
            new StaffMember { Id = "stf_Q8jWp2KvLn", FirstName = "Jordan", LastName = "Lee", Role = "Pro Shop", Email = "jordan@fairwayhq.example", Phone = "555-0277", HourlyRate = 22m, Active = true }
        );

        db.Shifts.AddRange(
            new Shift { Id = "shft_G3kPm7XqVj", StaffId = "stf_N7kVp3JqMx", Date = D(0), Start = "07:00", End = "15:00", Notes = "Lesson block 10-12" },
            new Shift { Id = "shft_T5nRw9BLpH", StaffId = "stf_Z4hRm9BwTf", Date = D(0), Start = "05:00", End = "13:00", Notes = "Mow greens & fairways" },
            new Shift { Id = "shft_H8jQx2MvKf", StaffId = "stf_Q8jWp2KvLn", Date = D(1), Start = "08:00", End = "17:00", Notes = "Cover Pro Shop" }
        );

        db.WeeklyTemplates.AddRange(
            new WeeklyTemplate { Id = "wtmp_A1bC2dE3fG", StaffId = "stf_N7kVp3JqMx", DayOfWeek = 1, Start = "07:00", End = "15:00", Notes = "Lessons 10-12" },
            new WeeklyTemplate { Id = "wtmp_H4jK5lM6nP", StaffId = "stf_N7kVp3JqMx", DayOfWeek = 2, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_Q7rS8tU9vW", StaffId = "stf_N7kVp3JqMx", DayOfWeek = 4, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_X2yZ3aB4cD", StaffId = "stf_N7kVp3JqMx", DayOfWeek = 5, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_E5fG6hJ7kL", StaffId = "stf_Z4hRm9BwTf", DayOfWeek = 1, Start = "05:00", End = "13:00", Notes = "Mow greens" },
            new WeeklyTemplate { Id = "wtmp_M8nP9qR2sT", StaffId = "stf_Z4hRm9BwTf", DayOfWeek = 3, Start = "05:00", End = "13:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_U3vW4xY5zA", StaffId = "stf_Z4hRm9BwTf", DayOfWeek = 5, Start = "05:00", End = "13:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_B6cD7eF8gH", StaffId = "stf_Z4hRm9BwTf", DayOfWeek = 6, Start = "05:00", End = "13:00", Notes = "Weekend prep" },
            new WeeklyTemplate { Id = "wtmp_J9kL2mN3pQ", StaffId = "stf_Q8jWp2KvLn", DayOfWeek = 2, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_R4sT5uV6wX", StaffId = "stf_Q8jWp2KvLn", DayOfWeek = 3, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_Y7zA8bC9dE", StaffId = "stf_Q8jWp2KvLn", DayOfWeek = 4, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_F2gH3jK4lM", StaffId = "stf_Q8jWp2KvLn", DayOfWeek = 5, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wtmp_N5pQ6rS7tU", StaffId = "stf_Q8jWp2KvLn", DayOfWeek = 6, Start = "08:00", End = "17:00", Notes = "" }
        );

        db.Products.AddRange(
            // Balls — Tour-grade
            new Product { Id = "prod_K2nM8wQjLp", Name = "Titleist Pro V1 Dozen", Category = "Balls", Sku = "TIT-PV1-12", Price = 54.99m, Cost = 36m, Stock = 42, ReorderLevel = 12 },
            new Product { Id = "prod_F4hLn7QxBg", Name = "Titleist Pro V1x Sleeve", Category = "Balls", Sku = "TIT-PV1X-3", Price = 17.99m, Cost = 9m, Stock = 60, ReorderLevel = 24 },
            new Product { Id = "prod_X8nGq2VfHr", Name = "Callaway Chrome Tour Dozen", Category = "Balls", Sku = "CAL-CHT-12", Price = 54.99m, Cost = 36m, Stock = 30, ReorderLevel = 12 },
            new Product { Id = "prod_B5jMw9YpLt", Name = "TaylorMade TP5 Dozen", Category = "Balls", Sku = "TM-TP5-12", Price = 54.99m, Cost = 36m, Stock = 28, ReorderLevel = 12 },
            new Product { Id = "prod_C3kVx7HnQf", Name = "TaylorMade TP5x Dozen", Category = "Balls", Sku = "TM-TP5X-12", Price = 54.99m, Cost = 36m, Stock = 18, ReorderLevel = 12 },
            new Product { Id = "prod_D2pYn4WmRj", Name = "Bridgestone Tour B XS Dozen", Category = "Balls", Sku = "BRG-BXS-12", Price = 49.99m, Cost = 32m, Stock = 14, ReorderLevel = 12 },
            new Product { Id = "prod_G8hLb6KqTx", Name = "Srixon Z-Star Dozen", Category = "Balls", Sku = "SRX-ZST-12", Price = 44.99m, Cost = 28m, Stock = 24, ReorderLevel = 12 },

            // Gloves — Tour-grade (right-handed players wear LH glove; sizes M/L/XL + Cadet)
            new Product { Id = "prod_J9pYm2KwVc", Name = "Premium Glove L", Category = "Accessories", Sku = "ACC-GLV-L", Price = 24.99m, Cost = 9m, Stock = 4, ReorderLevel = 10 },
            new Product { Id = "prod_H4mNw3VfYp", Name = "FootJoy Pure Touch Limited Glove M", Category = "Accessories", Sku = "FJ-PTL-M", Price = 36m, Cost = 16m, Stock = 8, ReorderLevel = 6 },
            new Product { Id = "prod_J6tQx9BgLk", Name = "FootJoy Pure Touch Limited Glove L", Category = "Accessories", Sku = "FJ-PTL-L", Price = 36m, Cost = 16m, Stock = 10, ReorderLevel = 6 },
            new Product { Id = "prod_K8nMv2WrHp", Name = "FootJoy Pure Touch Limited Glove XL", Category = "Accessories", Sku = "FJ-PTL-XL", Price = 36m, Cost = 16m, Stock = 3, ReorderLevel = 6 },
            new Product { Id = "prod_L3pYf7QjVx", Name = "FootJoy Pure Touch Limited Glove ML Cadet", Category = "Accessories", Sku = "FJ-PTL-MLC", Price = 36m, Cost = 16m, Stock = 5, ReorderLevel = 4 },
            new Product { Id = "prod_M9wBn4KhRt", Name = "Titleist Players Glove M", Category = "Accessories", Sku = "TIT-PLR-M", Price = 25m, Cost = 11m, Stock = 12, ReorderLevel = 8 },
            new Product { Id = "prod_N2tHk6XfYp", Name = "Titleist Players Glove L", Category = "Accessories", Sku = "TIT-PLR-L", Price = 25m, Cost = 11m, Stock = 14, ReorderLevel = 8 },
            new Product { Id = "prod_P5vRq8WmLg", Name = "Callaway Tour Authentic Glove L", Category = "Accessories", Sku = "CAL-TA-L", Price = 22m, Cost = 10m, Stock = 10, ReorderLevel = 6 },

            // Tees — Pro-style wooden + low-friction
            new Product { Id = "prod_Q7nMx3JhTd", Name = "Pride PTS 2-3/4\" Tees - White (50pk)", Category = "Accessories", Sku = "PRD-PTS-275-W", Price = 5.99m, Cost = 2.50m, Stock = 60, ReorderLevel = 24 },
            new Product { Id = "prod_R4kBp9YfVc", Name = "Pride PTS 3-1/4\" Tees - Natural (50pk)", Category = "Accessories", Sku = "PRD-PTS-325-N", Price = 6.99m, Cost = 3m, Stock = 40, ReorderLevel = 24 },
            new Product { Id = "prod_S6tWj2HmLn", Name = "Pride Performance 1-1/2\" Iron Tees (75pk)", Category = "Accessories", Sku = "PRD-PRF-150", Price = 3.99m, Cost = 1.60m, Stock = 50, ReorderLevel = 24 },
            new Product { Id = "prod_T8nQv5KqRx", Name = "Zero Friction 3-Prong Tour Tees (40pk)", Category = "Accessories", Sku = "ZF-3PT-40", Price = 9.99m, Cost = 4.20m, Stock = 28, ReorderLevel = 12 },

            // Apparel & F&B (existing)
            new Product { Id = "prod_R7vBx4HfTd", Name = "Club Polo - Navy", Category = "Apparel", Sku = "APP-POLO-NV", Price = 79m, Cost = 38m, Stock = 18, ReorderLevel = 6 },
            new Product { Id = "prod_W6tRd3JmPk", Name = "Iced Tea (Can)", Category = "Food & Beverage", Sku = "FB-ICED-TEA", Price = 3.50m, Cost = 0.80m, Stock = 96, ReorderLevel = 24 }
        );

        db.Tournaments.AddRange(
            new Tournament { Id = "trn_H3kMp7XqVj", Name = "Member-Guest Invitational", Date = D(14), Format = "Best Ball", CourseId = "crs_K7nMpQjLxR", EntryFee = 350m, MaxPlayers = 72, RegisteredJson = "[\"mbr_J4nKp2vQ8x\",\"mbr_P3xYm5dBqV\"]", Status = "Scheduled" },
            new Tournament { Id = "trn_P5nRw2BLqT", Name = "Friday Scramble", Date = D(4), Format = "Scramble", CourseId = "crs_K7nMpQjLxR", EntryFee = 60m, MaxPlayers = 40, RegisteredJson = "[\"mbr_W7gHk9rTfL\",\"mbr_F8jRn2KwHt\"]", Status = "Scheduled" }
        );

        db.Maintenance.AddRange(
            new MaintenanceTask { Id = "mnt_A4hKp9YqVx", Title = "Top-dress greens 1-9", Category = "Greens", CourseId = "crs_K7nMpQjLxR", AssignedTo = "stf_Z4hRm9BwTf", DueDate = D(2), Priority = "High", Status = "Open", Notes = "After morning play - coordinate with starter" },
            new MaintenanceTask { Id = "mnt_F7nMw3JqLt", Title = "Replace cup liners", Category = "Greens", CourseId = "crs_R3vBxHfTd2", AssignedTo = "stf_Z4hRm9BwTf", DueDate = D(1), Priority = "Medium", Status = "In Progress", Notes = "" },
            new MaintenanceTask { Id = "mnt_R2pYx5KhTd", Title = "Rake bunkers - back nine", Category = "Bunker", CourseId = "crs_K7nMpQjLxR", AssignedTo = "stf_Z4hRm9BwTf", DueDate = D(0), Priority = "Low", Status = "Open", Notes = "" }
        );

        var tab = new PlayerTab
        {
            Id = "tab_M9kPn4WqLx",
            OpenedAt = $"{D(0)}T08:05:00Z",
            Status = "Open",
            MemberIdsJson = "[\"mbr_J4nKp2vQ8x\",\"mbr_P3xYm5dBqV\"]",
            GuestsJson = "[]",
            TeeTimeId = "tee_A2nKp7vYjH",
            TipAmount = 0m,
            TaxRate = 0.0825m,
            Notes = ""
        };
        tab.Items.Add(new TabLineItem { Id = "line_J3hKp7XnVf", TabId = "tab_M9kPn4WqLx", ProductId = "prod_W6tRd3JmPk", Name = "Iced Tea (Can)", UnitPrice = 3.50m, Quantity = 2, Notes = "", AddedAt = $"{D(0)}T08:10:00Z" });
        tab.Items.Add(new TabLineItem { Id = "line_R8nMw2QqLt", TabId = "tab_M9kPn4WqLx", ProductId = "prod_K2nM8wQjLp", Name = "Pro V1 Dozen", UnitPrice = 54.99m, Quantity = 1, Notes = "Lost ball on 7", AddedAt = $"{D(0)}T09:30:00Z" });
        db.Tabs.Add(tab);

        db.MemberApplications.AddRange(
            new MemberApplication
            {
                Id = "app_K7nMpQjLxR",
                FirstName = "Quinn",
                LastName = "Harlow",
                Email = "quinn.harlow@example.com",
                Phone = "555-0901",
                RequestedTier = "Full",
                SponsoringMemberId = "mbr_P3xYm5dBqV",
                InitiationFee = 2500m,
                Notes = "Referred by S. Alvarez. Plays to ~10.",
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow.AddDays(-2).ToString("o"),
            },
            new MemberApplication
            {
                Id = "app_R3vBxHfTd2",
                FirstName = "Hana",
                LastName = "Okafor",
                Email = "hana.okafor@example.com",
                Phone = "555-0922",
                RequestedTier = "Weekday",
                InitiationFee = 1200m,
                Notes = "Self-applied via website form.",
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow.AddDays(-5).ToString("o"),
            }
        );

        db.SaveChanges();
    }
}
