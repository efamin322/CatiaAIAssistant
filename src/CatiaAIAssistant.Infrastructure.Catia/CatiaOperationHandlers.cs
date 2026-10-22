using CatiaAIAssistant.Core.Commands;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    /// <summary>
    /// CATIA V5 implementations of the allowed CAD operations. To add an operation, add ONE entry here
    /// (descriptor + call); <see cref="CadPlanExecutor"/> and the engineering layer stay unchanged.
    /// </summary>
    public static class CatiaOperationHandlers
    {
        public static CadOperationRegistry CreateRegistry(CatiaMechanicalEngine engine, CatiaBasicPartService parts)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(parts);
            var reg = new CadOperationRegistry();

            reg.Register(Part("CreateBlock", "بلوک مکعبی (Sketch + Pad)", new[] { "X", "Y", "Z" },
                c => parts.CreateBlockPart(c.Input("X"), c.Input("Y"), c.Input("Z"), c.TargetPath)));
            reg.Register(Part("CreateSteppedShaft", "شفت پله‌ای سه‌مقطعی (Shaft از پروفیل)", new[] { "D1", "L1", "D2", "L2", "D3", "L3" },
                c => engine.CreateSteppedShaftPart(c.Input("D1"), c.Input("L1"), c.Input("D2"), c.Input("L2"), c.Input("D3"), c.Input("L3"), c.TargetPath)));
            reg.Register(Part("CreateSpurGear", "چرخ‌دندهٔ ساده", new[] { "Module", "Teeth", "FaceWidth", "BoreDiameter" },
                c => engine.CreateSpurGearPart(c.Input("Module"), (int)Math.Round(c.Input("Teeth")), c.Input("FaceWidth"), c.Input("BoreDiameter"), c.TargetPath)));
            reg.Register(Part("CreateFlangeCoupling", "کوپلینگ فلنجی", new[] { "ShaftBore" },
                c => engine.CreateFlangeCouplingPart(c.Input("ShaftBore"), c.TargetPath)));
            reg.Register(Part("CreateCrankshaft", "میل‌لنگ ساده", new[] { "CrankRadius" },
                c => engine.CreateCrankshaftPart(c.Input("CrankRadius"), c.TargetPath)));
            reg.Register(Part("CreateConnectingRod", "شاتون", new[] { "RodLength" },
                c => engine.CreateConnectingRodPart(c.Input("RodLength"), c.TargetPath)));
            reg.Register(Part("CreatePiston", "پیستون", new[] { "PistonDiameter" },
                c => engine.CreatePistonPart(c.Input("PistonDiameter"), c.TargetPath)));
            reg.Register(Part("CreateQuadcopterFrame", "فریم کوادکوپتر", new[] { "Wheelbase" },
                c => engine.CreateQuadcopterFramePart(c.Input("Wheelbase"), c.TargetPath)));
            reg.Register(Part("CreateMotorDummy", "مدل سادهٔ موتور (حجم اشغالی)", Array.Empty<string>(),
                c => parts.CreateShapePart("دایره", 14.0, 0, 0, 26.0, c.TargetPath)));
            reg.Register(Part("CreateCarChassis", "شاسی ماشین اسباب‌بازی", new[] { "Length", "Width" },
                c => parts.CreateBlockPart(c.Input("Length"), c.Input("Width"), 12.0, c.TargetPath)));
            reg.Register(Part("CreateCarWheel", "چرخ ماشین اسباب‌بازی", new[] { "WheelDiameter" },
                c => parts.CreateShapePart("دایره", c.Input("WheelDiameter") / 2.0, 0, 0, 14.0, c.TargetPath)));

            reg.Register(Assembly("AssembleGearPair", "مونتاژ جفت چرخ‌دنده", new[] { "CenterDistance" }, new[] { "STEP_GEAR_1", "STEP_GEAR_2" },
                c => engine.CreateMultiPartAssembly("GearPair", new[]
                {
                    (c.OutputOf("STEP_GEAR_1"), "Pinion_1", 0.0, 0.0, 0.0),
                    (c.OutputOf("STEP_GEAR_2"), "Gear_2", c.Input("CenterDistance"), 0.0, 0.0)
                }, c.TargetPath)));

            reg.Register(Assembly("AssembleCrankSlider", "مونتاژ مکانیزم لنگ-لغزنده", new[] { "CrankRadius", "RodLength" }, new[] { "STEP_CRANK", "STEP_ROD", "STEP_PISTON" },
                c =>
                {
                    double r = c.Input("CrankRadius"), l = c.Input("RodLength");
                    return engine.CreateMultiPartAssembly("CrankSlider", new[]
                    {
                        (c.OutputOf("STEP_CRANK"), "Crank", 0.0, 0.0, 0.0),
                        (c.OutputOf("STEP_ROD"), "Rod", r, 0.0, 12.0),
                        (c.OutputOf("STEP_PISTON"), "Piston", r + l, 0.0, 12.0)
                    }, c.TargetPath);
                }));

            reg.Register(Assembly("AssembleQuadcopter", "مونتاژ کوادکوپتر", new[] { "Wheelbase" }, new[] { "STEP_QUAD_FRAME", "STEP_QUAD_MOTOR" },
                c =>
                {
                    double off = c.Input("Wheelbase") / 2.0 * 0.7071;
                    string f = c.OutputOf("STEP_QUAD_FRAME"), m = c.OutputOf("STEP_QUAD_MOTOR");
                    return engine.CreateMultiPartAssembly("Quadcopter", new[]
                    {
                        (f, "MainFrame", 0.0, 0.0, 0.0),
                        (m, "Motor_1", off, off, 8.0),
                        (m, "Motor_2", -off, off, 8.0),
                        (m, "Motor_3", -off, -off, 8.0),
                        (m, "Motor_4", off, -off, 8.0)
                    }, c.TargetPath);
                }));

            reg.Register(Assembly("AssembleToyCar", "مونتاژ ماشین اسباب‌بازی", new[] { "Length", "Width" }, new[] { "STEP_CAR_CHASSIS", "STEP_CAR_WHEEL" },
                c =>
                {
                    double cl = c.Input("Length"), cw = c.Input("Width");
                    string cf = c.OutputOf("STEP_CAR_CHASSIS"), wf = c.OutputOf("STEP_CAR_WHEEL");
                    return engine.CreateMultiPartAssembly("ToyCar", new[]
                    {
                        (cf, "Chassis", 0.0, 0.0, 0.0),
                        (wf, "Wheel_FL", cl * 0.2, -15.0, 5.0),
                        (wf, "Wheel_FR", cl * 0.2, cw + 2.0, 5.0),
                        (wf, "Wheel_RL", cl * 0.8, -15.0, 5.0),
                        (wf, "Wheel_RR", cl * 0.8, cw + 2.0, 5.0)
                    }, c.TargetPath);
                }));

            return reg;
        }

        private static ICadOperationHandler Part(string type, string description, string[] inputs, Func<CadStepContext, Core.Logging.CadOperationResult> run) =>
            new DelegateCadOperationHandler(new CadOperationDescriptor
            {
                OperationType = type, Description = description, RequiredInputs = inputs,
                Output = OperationOutputKind.Part, Risk = OperationRisk.Low, Undoable = true
            }, run);

        private static ICadOperationHandler Assembly(string type, string description, string[] inputs, string[] prerequisites, Func<CadStepContext, Core.Logging.CadOperationResult> run) =>
            new DelegateCadOperationHandler(new CadOperationDescriptor
            {
                OperationType = type, Description = description, RequiredInputs = inputs, RequiredPrerequisiteOutputs = prerequisites,
                Output = OperationOutputKind.Product, Risk = OperationRisk.Medium, Undoable = true
            }, run);
    }
}
