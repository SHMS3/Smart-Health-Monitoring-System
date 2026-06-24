using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodHabitsToPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DietBalanced",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DrinkEnoughWater",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExerciseRegularly",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NoSubstanceAbuse",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RegularHealthCheck",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SleepEarly",
                table: "PatientHabit",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DietBalanced",
                table: "PatientHabit");

            migrationBuilder.DropColumn(
                name: "DrinkEnoughWater",
                table: "PatientHabit");

            migrationBuilder.DropColumn(
                name: "ExerciseRegularly",
                table: "PatientHabit");

            migrationBuilder.DropColumn(
                name: "NoSubstanceAbuse",
                table: "PatientHabit");

            migrationBuilder.DropColumn(
                name: "RegularHealthCheck",
                table: "PatientHabit");

            migrationBuilder.DropColumn(
                name: "SleepEarly",
                table: "PatientHabit");
        }
    }
}
