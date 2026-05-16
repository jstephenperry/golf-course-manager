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
            new Course { Id = "c1", Name = "Championship Course", Holes = 18, Par = 72, Yardage = 7124, Rating = 74.2, Slope = 138, Status = "Open", OpenTime = "06:00", CloseTime = "18:00", Notes = "Signature layout with island green on 17." },
            new Course { Id = "c2", Name = "Heritage Nine", Holes = 9, Par = 35, Yardage = 3210, Rating = 35.8, Slope = 122, Status = "Open", OpenTime = "07:00", CloseTime = "17:00", Notes = "Walking-only short course, great for juniors." },
            new Course { Id = "c3", Name = "Practice Range", Holes = 0, Par = 0, Yardage = 0, Rating = 0, Slope = 0, Status = "Open", OpenTime = "06:00", CloseTime = "20:00", Notes = "Driving range, short-game area, and putting green." }
        );

        db.Members.AddRange(
            new Member { Id = "m1", FirstName = "Eleanor", LastName = "Park", Email = "eleanor.park@example.com", Phone = "555-0142", Tier = "Full", Handicap = 8.4, JoinDate = "2019-03-12", Active = true, Balance = 0m },
            new Member { Id = "m2", FirstName = "Marcus", LastName = "Reed", Email = "marcus.reed@example.com", Phone = "555-0188", Tier = "Weekday", Handicap = 14.2, JoinDate = "2021-07-01", Active = true, Balance = 125.50m },
            new Member { Id = "m3", FirstName = "Sofia", LastName = "Alvarez", Email = "sofia.alvarez@example.com", Phone = "555-0114", Tier = "Full", Handicap = 3.1, JoinDate = "2015-05-22", Active = true, Balance = 0m },
            new Member { Id = "m4", FirstName = "Daniel", LastName = "O'Connell", Email = "danny.oc@example.com", Phone = "555-0199", Tier = "Corporate", Handicap = 22.6, JoinDate = "2023-01-10", Active = true, Balance = 320m },
            new Member { Id = "m5", FirstName = "Priya", LastName = "Shah", Email = "priya.shah@example.com", Phone = "555-0167", Tier = "Social", Handicap = 0, JoinDate = "2022-09-30", Active = false, Balance = 0m }
        );

        db.TeeTimes.AddRange(
            new TeeTime { Id = "t1", Date = D(0), Time = "08:00", CourseId = "c1", PlayersJson = "[\"m1\",\"m3\"]", Cart = true, Status = "Booked", Notes = "" },
            new TeeTime { Id = "t2", Date = D(0), Time = "08:15", CourseId = "c1", PlayersJson = "[\"m2\"]", Cart = false, Status = "Booked", Notes = "Walking with caddie" },
            new TeeTime { Id = "t3", Date = D(1), Time = "10:30", CourseId = "c2", PlayersJson = "[\"m4\"]", Cart = true, Status = "Booked", Notes = "Corporate guest, send cart staff" }
        );

        db.Staff.AddRange(
            new StaffMember { Id = "s1", FirstName = "Theo", LastName = "Nakamura", Role = "Pro", Email = "theo@fairwayhq.example", Phone = "555-0211", HourlyRate = 65m, Active = true },
            new StaffMember { Id = "s2", FirstName = "Beatrice", LastName = "Long", Role = "Greenkeeper", Email = "bea@fairwayhq.example", Phone = "555-0233", HourlyRate = 32m, Active = true },
            new StaffMember { Id = "s3", FirstName = "Jordan", LastName = "Lee", Role = "Pro Shop", Email = "jordan@fairwayhq.example", Phone = "555-0277", HourlyRate = 22m, Active = true }
        );

        db.Shifts.AddRange(
            new Shift { Id = "sh1", StaffId = "s1", Date = D(0), Start = "07:00", End = "15:00", Notes = "Lesson block 10-12" },
            new Shift { Id = "sh2", StaffId = "s2", Date = D(0), Start = "05:00", End = "13:00", Notes = "Mow greens & fairways" },
            new Shift { Id = "sh3", StaffId = "s3", Date = D(1), Start = "08:00", End = "17:00", Notes = "Cover Pro Shop" }
        );

        db.WeeklyTemplates.AddRange(
            new WeeklyTemplate { Id = "wt1", StaffId = "s1", DayOfWeek = 1, Start = "07:00", End = "15:00", Notes = "Lessons 10-12" },
            new WeeklyTemplate { Id = "wt2", StaffId = "s1", DayOfWeek = 2, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wt3", StaffId = "s1", DayOfWeek = 4, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wt4", StaffId = "s1", DayOfWeek = 5, Start = "07:00", End = "15:00", Notes = "" },
            new WeeklyTemplate { Id = "wt5", StaffId = "s2", DayOfWeek = 1, Start = "05:00", End = "13:00", Notes = "Mow greens" },
            new WeeklyTemplate { Id = "wt6", StaffId = "s2", DayOfWeek = 3, Start = "05:00", End = "13:00", Notes = "" },
            new WeeklyTemplate { Id = "wt7", StaffId = "s2", DayOfWeek = 5, Start = "05:00", End = "13:00", Notes = "" },
            new WeeklyTemplate { Id = "wt8", StaffId = "s2", DayOfWeek = 6, Start = "05:00", End = "13:00", Notes = "Weekend prep" },
            new WeeklyTemplate { Id = "wt9", StaffId = "s3", DayOfWeek = 2, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wt10", StaffId = "s3", DayOfWeek = 3, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wt11", StaffId = "s3", DayOfWeek = 4, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wt12", StaffId = "s3", DayOfWeek = 5, Start = "08:00", End = "17:00", Notes = "" },
            new WeeklyTemplate { Id = "wt13", StaffId = "s3", DayOfWeek = 6, Start = "08:00", End = "17:00", Notes = "" }
        );

        db.Products.AddRange(
            new Product { Id = "p1", Name = "Pro V1 Dozen", Category = "Balls", Sku = "TIT-PV1-12", Price = 54.99m, Cost = 36m, Stock = 42, ReorderLevel = 12 },
            new Product { Id = "p2", Name = "Club Polo - Navy", Category = "Apparel", Sku = "APP-POLO-NV", Price = 79m, Cost = 38m, Stock = 18, ReorderLevel = 6 },
            new Product { Id = "p3", Name = "Premium Glove L", Category = "Accessories", Sku = "ACC-GLV-L", Price = 24.99m, Cost = 9m, Stock = 4, ReorderLevel = 10 },
            new Product { Id = "p4", Name = "Sleeve of Pro V1x", Category = "Balls", Sku = "TIT-PV1X-3", Price = 17.99m, Cost = 9m, Stock = 60, ReorderLevel = 24 },
            new Product { Id = "p5", Name = "Iced Tea (Can)", Category = "Food & Beverage", Sku = "FB-ICED-TEA", Price = 3.50m, Cost = 0.80m, Stock = 96, ReorderLevel = 24 }
        );

        db.Tournaments.AddRange(
            new Tournament { Id = "tr1", Name = "Member-Guest Invitational", Date = D(14), Format = "Best Ball", CourseId = "c1", EntryFee = 350m, MaxPlayers = 72, RegisteredJson = "[\"m1\",\"m3\"]", Status = "Scheduled" },
            new Tournament { Id = "tr2", Name = "Friday Scramble", Date = D(4), Format = "Scramble", CourseId = "c1", EntryFee = 60m, MaxPlayers = 40, RegisteredJson = "[\"m2\",\"m4\"]", Status = "Scheduled" }
        );

        db.Maintenance.AddRange(
            new MaintenanceTask { Id = "mt1", Title = "Top-dress greens 1-9", Category = "Greens", CourseId = "c1", AssignedTo = "s2", DueDate = D(2), Priority = "High", Status = "Open", Notes = "After morning play - coordinate with starter" },
            new MaintenanceTask { Id = "mt2", Title = "Replace cup liners", Category = "Greens", CourseId = "c2", AssignedTo = "s2", DueDate = D(1), Priority = "Medium", Status = "In Progress", Notes = "" },
            new MaintenanceTask { Id = "mt3", Title = "Rake bunkers - back nine", Category = "Bunker", CourseId = "c1", AssignedTo = "s2", DueDate = D(0), Priority = "Low", Status = "Open", Notes = "" }
        );

        var tab = new PlayerTab
        {
            Id = "tab1",
            OpenedAt = $"{D(0)}T08:05:00Z",
            Status = "Open",
            MemberIdsJson = "[\"m1\",\"m3\"]",
            GuestsJson = "[]",
            TeeTimeId = "t1",
            TipAmount = 0m,
            TaxRate = 0.0825m,
            Notes = ""
        };
        tab.Items.Add(new TabLineItem { Id = "li1", TabId = "tab1", ProductId = "p5", Name = "Iced Tea (Can)", UnitPrice = 3.50m, Quantity = 2, Notes = "", AddedAt = $"{D(0)}T08:10:00Z" });
        tab.Items.Add(new TabLineItem { Id = "li2", TabId = "tab1", ProductId = "p1", Name = "Pro V1 Dozen", UnitPrice = 54.99m, Quantity = 1, Notes = "Lost ball on 7", AddedAt = $"{D(0)}T09:30:00Z" });
        db.Tabs.Add(tab);

        db.SaveChanges();
    }
}
