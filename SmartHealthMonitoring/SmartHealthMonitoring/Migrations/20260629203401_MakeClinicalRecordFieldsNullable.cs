using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHealthMonitoring.Migrations
{
    /// <inheritdoc />
    public partial class MakeClinicalRecordFieldsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "ThalResult",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<byte>(
                name: "STSlope",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<short>(
                name: "RestingBP",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<byte>(
                name: "RestECG",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<decimal>(
                name: "OldPeak",
                table: "ClinicalRecords",
                type: "decimal(4,1)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(4,1)");

            migrationBuilder.AlterColumn<short>(
                name: "MaxHeartRate",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<byte>(
                name: "MajorVessels",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<byte>(
                name: "FastingBS",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<byte>(
                name: "ExerciseAngina",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");

            migrationBuilder.AlterColumn<short>(
                name: "Cholesterol",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<byte>(
                name: "ChestPainType",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: true,
                oldClrType: typeof(byte),
                oldType: "tinyint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "ThalResult",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "STSlope",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "RestingBP",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "RestECG",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OldPeak",
                table: "ClinicalRecords",
                type: "decimal(4,1)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(4,1)",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "MaxHeartRate",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "MajorVessels",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "FastingBS",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "ExerciseAngina",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "Cholesterol",
                table: "ClinicalRecords",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "ChestPainType",
                table: "ClinicalRecords",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0,
                oldClrType: typeof(byte),
                oldType: "tinyint",
                oldNullable: true);
        }
    }
}
