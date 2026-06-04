using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SMath = System.Math;

namespace EdaSimulator.Engines.PCB
{
    public class PcbStepExporter
    {
        public static string ExportToStep(PcbDocument board)
        {
            var sb = new StringBuilder();

            // STEP file header
            sb.AppendLine("ISO-10303-21;");
            sb.AppendLine("HEADER;");
            sb.AppendLine("FILE_DESCRIPTION(('EDA PCB 3D Assembly model'),'2;1');");
            sb.AppendLine($"FILE_NAME('{board.Title}.step','{DateTime.Now:yyyy-MM-ddTHH:mm:ss}',('Antigravity'),('Google Deepmind'),'EDA CAD Simulator','EDA CAD Simulator','');");
            sb.AppendLine("FILE_SCHEMA(('CONFIG_CONTROL_DESIGN'));");
            sb.AppendLine("ENDSEC;");
            sb.AppendLine("DATA;");

            int idCounter = 10;

            // Define global geometry entities
            int dirUXId = idCounter++;
            int dirUYId = idCounter++;
            int dirUZId = idCounter++;
            int vecUXId = idCounter++;
            int vecUYId = idCounter++;
            int vecUZId = idCounter++;

            sb.AppendLine($"#{dirUXId} = DIRECTION('', (1.0, 0.0, 0.0));");
            sb.AppendLine($"#{dirUYId} = DIRECTION('', (0.0, 1.0, 0.0));");
            sb.AppendLine($"#{dirUZId} = DIRECTION('', (0.0, 0.0, 1.0));");
            sb.AppendLine($"#{vecUXId} = VECTOR('', #{dirUXId}, 1.0);");
            sb.AppendLine($"#{vecUYId} = VECTOR('', #{dirUYId}, 1.0);");
            sb.AppendLine($"#{vecUZId} = VECTOR('', #{dirUZId}, 1.0);");

            // List of manifold solid B-rep IDs
            var solidIds = new List<int>();

            // 1. Board Substrate
            // Board outline is Outline.Width_mm x Outline.Height_mm x 1.6mm thickness
            double pcbW = board.Outline.Width_mm;
            double pcbH = board.Outline.Height_mm;
            double pcbThick = 1.6;

            int boardSolidId = GenerateStepCuboid(ref idCounter, sb, pcbW / 2.0, pcbH / 2.0, pcbThick / 2.0, pcbW, pcbH, pcbThick, 0, "PCB_Substrate");
            solidIds.Add(boardSolidId);

            // 2. Footprints / Components
            foreach (var f in board.Footprints)
            {
                // Component local dimensions (fallback to 5x5x3 if zero)
                double w = f.CadWidth_mm > 0 ? f.CadWidth_mm : 5.0;
                double h = f.CadHeight_mm > 0 ? f.CadHeight_mm : 5.0;
                double d = f.CadDepth_mm > 0 ? f.CadDepth_mm : 3.0;

                // Center position (z is above PCB surface, board thickness is 1.6mm)
                double zCenter = pcbThick + d / 2.0;

                string label = $"{f.Designator}_{f.Value}".Replace(" ", "_").Replace("(", "").Replace(")", "");

                int compSolidId = GenerateStepCuboid(ref idCounter, sb, f.X, f.Y, zCenter, w, h, d, f.Rotation, label);
                solidIds.Add(compSolidId);
            }

            // 3. Assemble and map shapes to product structure
            int originPtId = idCounter++;
            int axisId = idCounter++;
            int wcsId = idCounter++;
            sb.AppendLine($"#{originPtId} = CARTESIAN_POINT('', (0.0, 0.0, 0.0));");
            sb.AppendLine($"#{axisId} = AXIS2_PLACEMENT_3D('', #{originPtId}, #{dirUZId}, #{dirUXId});");
            
            // Build advanced brep shape representation containing all solids
            int repId = idCounter++;
            var solidListStr = string.Join(", ", solidIds.ConvertAll(id => $"#{id}"));
            sb.AppendLine($"#{repId} = ADVANCED_BREP_SHAPE_REPRESENTATION('', ({solidListStr}, #{axisId}), #1);");

            // Context representation
            sb.AppendLine($"#1 = ( GEOMETRIC_REPRESENTATION_CONTEXT(3) GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#2)) GLOBAL_UNIT_ASSIGNED_CONTEXT((#3, #4, #5)) REPRESENTATION_CONTEXT('PCB Assembly', '3D') );");
            sb.AppendLine("#2 = UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.0E-05), #3, 'distance_accuracy_value', 'Maximum Tolerance');");
            sb.AppendLine("#3 = ( LENGTH_UNIT() NAMED_UNIT(*) SI_UNIT(.MILLI., .METRE.) );");
            sb.AppendLine("#4 = ( NAMED_UNIT(*) PLANE_ANGLE_UNIT() SI_UNIT($, .RADIAN.) );");
            sb.AppendLine("#5 = ( NAMED_UNIT(*) SI_UNIT($, .STERADIAN.) SOLID_ANGLE_UNIT() );");

            // Product definition structure to make CAD systems identify it as a valid product assemblies
            int prodId = idCounter++;
            int prodFormId = idCounter++;
            int prodDefId = idCounter++;
            int shapeAttrId = idCounter++;
            int productContextId = idCounter++;

            sb.AppendLine($"#{prodId} = PRODUCT('{board.Title}', '{board.Title}', '', (#{productContextId}));");
            sb.AppendLine($"#{productContextId} = PRODUCT_CONTEXT('', #6, 'mechanical');");
            sb.AppendLine("#6 = APPLICATION_CONTEXT('configuration controlled 3d designs of mechanical parts and assemblies');");
            sb.AppendLine($"#{prodFormId} = PRODUCT_DEFINITION_FORMATION('1', '', #{prodId});");
            sb.AppendLine($"#{prodDefId} = PRODUCT_DEFINITION('design', '', #{prodFormId}, #7);");
            sb.AppendLine("#7 = PRODUCT_DEFINITION_CONTEXT('part definition', #6, 'design');");
            sb.AppendLine($"#{shapeAttrId} = PRODUCT_DEFINITION_SHAPE('', '', #{prodDefId});");
            sb.AppendLine($"#{idCounter++} = SHAPE_DEFINITION_REPRESENTATION(#{shapeAttrId}, #{repId});");

            sb.AppendLine("ENDSEC;");
            sb.AppendLine("END-ISO-10303-21;");

            return sb.ToString();
        }

        private static int GenerateStepCuboid(ref int idCounter, StringBuilder sb, double Xc, double Yc, double Zc, double L, double W, double H, double thetaDegrees, string label)
        {
            double theta = thetaDegrees * SMath.PI / 180.0;

            double cos = SMath.Cos(theta);
            double sin = SMath.Sin(theta);

            // Local coordinates offset
            double halfL = L / 2.0;
            double halfW = W / 2.0;
            double halfH = H / 2.0;

            // Local corners relative to center of box (in XY plane, centered at bottom or center of box? Center of box)
            var localCorners = new[]
            {
                new { x = -halfL, y = -halfW, z = -halfH },
                new { x = halfL,  y = -halfW, z = -halfH },
                new { x = halfL,  y = halfW,  z = -halfH },
                new { x = -halfL, y = halfW,  z = -halfH },
                new { x = -halfL, y = -halfW, z = halfH },
                new { x = halfL,  y = -halfW, z = halfH },
                new { x = halfL,  y = halfW,  z = halfH },
                new { x = -halfL, y = halfW,  z = halfH }
            };

            // Calculate world corners
            int firstPtId = idCounter;
            for (int i = 0; i < 8; i++)
            {
                double xw = localCorners[i].x * cos - localCorners[i].y * sin + Xc;
                double yw = localCorners[i].x * sin + localCorners[i].y * cos + Yc;
                double zw = localCorners[i].z + Zc;

                sb.AppendLine($"#{idCounter++} = CARTESIAN_POINT('{label}_P{i}', ({xw:F5}, {yw:F5}, {zw:F5}));");
            }

            // Create 8 vertices
            int firstVertId = idCounter;
            for (int i = 0; i < 8; i++)
            {
                sb.AppendLine($"#{idCounter++} = VERTEX_POINT('{label}_V{i}', #{firstPtId + i});");
            }

            // Local directions in world space
            int dirUId = idCounter++;
            int dirVId = idCounter++;
            int dirWId = idCounter++;
            sb.AppendLine($"#{dirUId} = DIRECTION('', ({cos:F6}, {sin:F6}, 0.0));");
            sb.AppendLine($"#{dirVId} = DIRECTION('', ({-sin:F6}, {cos:F6}, 0.0));");
            sb.AppendLine($"#{dirWId} = DIRECTION('', (0.0, 0.0, 1.0));");

            int vecUId = idCounter++;
            int vecVId = idCounter++;
            int vecWId = idCounter++;
            sb.AppendLine($"#{vecUId} = VECTOR('', #{dirUId}, 1.0);");
            sb.AppendLine($"#{vecVId} = VECTOR('', #{dirVId}, 1.0);");
            sb.AppendLine($"#{vecWId} = VECTOR('', #{dirWId}, 1.0);");

            // Define the 12 lines
            int line0 = idCounter++; sb.AppendLine($"#{line0} = LINE('', #{firstPtId + 0}, #{vecUId});"); // P0 -> P1
            int line1 = idCounter++; sb.AppendLine($"#{line1} = LINE('', #{firstPtId + 1}, #{vecVId});"); // P1 -> P2
            int line2 = idCounter++; sb.AppendLine($"#{line2} = LINE('', #{firstPtId + 3}, #{vecUId});"); // P3 -> P2
            int line3 = idCounter++; sb.AppendLine($"#{line3} = LINE('', #{firstPtId + 0}, #{vecVId});"); // P0 -> P3
            
            int line4 = idCounter++; sb.AppendLine($"#{line4} = LINE('', #{firstPtId + 4}, #{vecUId});"); // P4 -> P5
            int line5 = idCounter++; sb.AppendLine($"#{line5} = LINE('', #{firstPtId + 5}, #{vecVId});"); // P5 -> P6
            int line6 = idCounter++; sb.AppendLine($"#{line6} = LINE('', #{firstPtId + 7}, #{vecUId});"); // P7 -> P6
            int line7 = idCounter++; sb.AppendLine($"#{line7} = LINE('', #{firstPtId + 4}, #{vecVId});"); // P4 -> P7

            int line8 = idCounter++; sb.AppendLine($"#{line8} = LINE('', #{firstPtId + 0}, #{vecWId});"); // P0 -> P4
            int line9 = idCounter++; sb.AppendLine($"#{line9} = LINE('', #{firstPtId + 1}, #{vecWId});"); // P1 -> P5
            int line10 = idCounter++; sb.AppendLine($"#{line10} = LINE('', #{firstPtId + 2}, #{vecWId});"); // P2 -> P6
            int line11 = idCounter++; sb.AppendLine($"#{line11} = LINE('', #{firstPtId + 3}, #{vecWId});"); // P3 -> P7

            // Create 12 edge curves
            int ec0 = idCounter++; sb.AppendLine($"#{ec0} = EDGE_CURVE('', #{firstVertId + 0}, #{firstVertId + 1}, #{line0}, .T.);");
            int ec1 = idCounter++; sb.AppendLine($"#{ec1} = EDGE_CURVE('', #{firstVertId + 1}, #{firstVertId + 2}, #{line1}, .T.);");
            int ec2 = idCounter++; sb.AppendLine($"#{ec2} = EDGE_CURVE('', #{firstVertId + 3}, #{firstVertId + 2}, #{line2}, .T.);");
            int ec3 = idCounter++; sb.AppendLine($"#{ec3} = EDGE_CURVE('', #{firstVertId + 0}, #{firstVertId + 3}, #{line3}, .T.);");

            int ec4 = idCounter++; sb.AppendLine($"#{ec4} = EDGE_CURVE('', #{firstVertId + 4}, #{firstVertId + 5}, #{line4}, .T.);");
            int ec5 = idCounter++; sb.AppendLine($"#{ec5} = EDGE_CURVE('', #{firstVertId + 5}, #{firstVertId + 6}, #{line5}, .T.);");
            int ec6 = idCounter++; sb.AppendLine($"#{ec6} = EDGE_CURVE('', #{firstVertId + 7}, #{firstVertId + 6}, #{line6}, .T.);");
            int ec7 = idCounter++; sb.AppendLine($"#{ec7} = EDGE_CURVE('', #{firstVertId + 4}, #{firstVertId + 7}, #{line7}, .T.);");

            int ec8 = idCounter++; sb.AppendLine($"#{ec8} = EDGE_CURVE('', #{firstVertId + 0}, #{firstVertId + 4}, #{line8}, .T.);");
            int ec9 = idCounter++; sb.AppendLine($"#{ec9} = EDGE_CURVE('', #{firstVertId + 1}, #{firstVertId + 5}, #{line9}, .T.);");
            int ec10 = idCounter++; sb.AppendLine($"#{ec10} = EDGE_CURVE('', #{firstVertId + 2}, #{firstVertId + 6}, #{line10}, .T.);");
            int ec11 = idCounter++; sb.AppendLine($"#{ec11} = EDGE_CURVE('', #{firstVertId + 3}, #{firstVertId + 7}, #{line11}, .T.);");

            // Oriented edges for each of the 6 loops
            int oeBottom0 = idCounter++; sb.AppendLine($"#{oeBottom0} = ORIENTED_EDGE('', *, *, #{ec3}, .F.);");
            int oeBottom1 = idCounter++; sb.AppendLine($"#{oeBottom1} = ORIENTED_EDGE('', *, *, #{ec2}, .F.);");
            int oeBottom2 = idCounter++; sb.AppendLine($"#{oeBottom2} = ORIENTED_EDGE('', *, *, #{ec1}, .F.);");
            int oeBottom3 = idCounter++; sb.AppendLine($"#{oeBottom3} = ORIENTED_EDGE('', *, *, #{ec0}, .F.);");
            int loopBottom = idCounter++; sb.AppendLine($"#{loopBottom} = EDGE_LOOP('', (#{oeBottom0}, #{oeBottom1}, #{oeBottom2}, #{oeBottom3}));");
            int bndBottom = idCounter++; sb.AppendLine($"#{bndBottom} = FACE_OUTER_BOUND('', #{loopBottom}, .T.);");

            int oeTop0 = idCounter++; sb.AppendLine($"#{oeTop0} = ORIENTED_EDGE('', *, *, #{ec4}, .T.);");
            int oeTop1 = idCounter++; sb.AppendLine($"#{oeTop1} = ORIENTED_EDGE('', *, *, #{ec5}, .T.);");
            int oeTop2 = idCounter++; sb.AppendLine($"#{oeTop2} = ORIENTED_EDGE('', *, *, #{ec6}, .F.);"); // E6 goes P7->P6, loop top is P5->P6->P7->P4. Wait, P5->P6 (+E5), P6->P7 (-E6), P7->P4 (-E7), P4->P5 (+E4). Wait, top loop: P4->P5->P6->P7->P4: +E4, +E5, -E6, -E7
            // Let's check: E4 is P4->P5 (.T.), E5 is P5->P6 (.T.), E6 is P7->P6 (.F. for P6->P7), E7 is P4->P7 (.F. for P7->P4).
            // Let's write oriented edges:
            int oeTop3 = idCounter++; sb.AppendLine($"#{oeTop3} = ORIENTED_EDGE('', *, *, #{ec7}, .F.);");
            // Re-order top oriented edges: oeTop0 (+E4), oeTop1 (+E5), oeTop2 (-E6), oeTop3 (-E7)
            // Let's write them:
            int loopTop = idCounter++; sb.AppendLine($"#{loopTop} = EDGE_LOOP('', (#{oeTop0}, #{oeTop1}, oeTop2_ref, oeTop3_ref));");
            // Wait, we can reference the variables directly:
            sb.Replace("oeTop2_ref", $"#{oeTop2}");
            sb.Replace("oeTop3_ref", $"#{oeTop3}");
            int bndTop = idCounter++; sb.AppendLine($"#{bndTop} = FACE_OUTER_BOUND('', #{loopTop}, .T.);");

            // Front loop: P0->P1->P5->P4->P0 (+E0, +E9, -E4, -E8)
            int oeFront0 = idCounter++; sb.AppendLine($"#{oeFront0} = ORIENTED_EDGE('', *, *, #{ec0}, .T.);");
            int oeFront1 = idCounter++; sb.AppendLine($"#{oeFront1} = ORIENTED_EDGE('', *, *, #{ec9}, .T.);");
            int oeFront2 = idCounter++; sb.AppendLine($"#{oeFront2} = ORIENTED_EDGE('', *, *, #{ec4}, .F.);");
            int oeFront3 = idCounter++; sb.AppendLine($"#{oeFront3} = ORIENTED_EDGE('', *, *, #{ec8}, .F.);");
            int loopFront = idCounter++; sb.AppendLine($"#{loopFront} = EDGE_LOOP('', (#{oeFront0}, #{oeFront1}, #{oeFront2}, #{oeFront3}));");
            int bndFront = idCounter++; sb.AppendLine($"#{bndFront} = FACE_OUTER_BOUND('', #{loopFront}, .T.);");

            // Back loop: P3->P7->P6->P2->P3 (+E11, -E6, -E10, +E2)
            int oeBack0 = idCounter++; sb.AppendLine($"#{oeBack0} = ORIENTED_EDGE('', *, *, #{ec11}, .T.);");
            int oeBack1 = idCounter++; sb.AppendLine($"#{oeBack1} = ORIENTED_EDGE('', *, *, #{ec6}, .F.);");
            int oeBack2 = idCounter++; sb.AppendLine($"#{oeBack2} = ORIENTED_EDGE('', *, *, #{ec10}, .F.);");
            int oeBack3 = idCounter++; sb.AppendLine($"#{oeBack3} = ORIENTED_EDGE('', *, *, #{ec2}, .T.);");
            int loopBack = idCounter++; sb.AppendLine($"#{loopBack} = EDGE_LOOP('', (#{oeBack0}, #{oeBack1}, #{oeBack2}, #{oeBack3}));");
            int bndBack = idCounter++; sb.AppendLine($"#{bndBack} = FACE_OUTER_BOUND('', #{loopBack}, .T.);");

            // Left loop: P0->P4->P7->P3->P0 (+E8, -E7, -E11, +E3)
            int oeLeft0 = idCounter++; sb.AppendLine($"#{oeLeft0} = ORIENTED_EDGE('', *, *, #{ec8}, .T.);");
            int oeLeft1 = idCounter++; sb.AppendLine($"#{oeLeft1} = ORIENTED_EDGE('', *, *, #{ec7}, .F.);");
            int oeLeft2 = idCounter++; sb.AppendLine($"#{oeLeft2} = ORIENTED_EDGE('', *, *, #{ec11}, .F.);");
            int oeLeft3 = idCounter++; sb.AppendLine($"#{oeLeft3} = ORIENTED_EDGE('', *, *, #{ec3}, .T.);");
            int loopLeft = idCounter++; sb.AppendLine($"#{loopLeft} = EDGE_LOOP('', (#{oeLeft0}, #{oeLeft1}, #{oeLeft2}, #{oeLeft3}));");
            int bndLeft = idCounter++; sb.AppendLine($"#{bndLeft} = FACE_OUTER_BOUND('', #{loopLeft}, .T.);");

            // Right loop: P1->P2->P6->P5->P1 (+E1, +E10, -E5, -E9)
            int oeRight0 = idCounter++; sb.AppendLine($"#{oeRight0} = ORIENTED_EDGE('', *, *, #{ec1}, .T.);");
            int oeRight1 = idCounter++; sb.AppendLine($"#{oeRight1} = ORIENTED_EDGE('', *, *, #{ec10}, .T.);");
            int oeRight2 = idCounter++; sb.AppendLine($"#{oeRight2} = ORIENTED_EDGE('', *, *, #{ec5}, .F.);");
            int oeRight3 = idCounter++; sb.AppendLine($"#{oeRight3} = ORIENTED_EDGE('', *, *, #{ec9}, .F.);");
            int loopRight = idCounter++; sb.AppendLine($"#{loopRight} = EDGE_LOOP('', (#{oeRight0}, #{oeRight1}, #{oeRight2}, #{oeRight3}));");
            int bndRight = idCounter++; sb.AppendLine($"#{bndRight} = FACE_OUTER_BOUND('', #{loopRight}, .T.);");

            // Planes and advanced faces for each of the 6 faces
            int dirWNegId = idCounter++; sb.AppendLine($"#{dirWNegId} = DIRECTION('', (0.0, 0.0, -1.0));");
            int dirWPosId = idCounter++; sb.AppendLine($"#{dirWPosId} = DIRECTION('', (0.0, 0.0, 1.0));");

            int dirVNegId = idCounter++; sb.AppendLine($"#{dirVNegId} = DIRECTION('', ({sin:F6}, {-cos:F6}, 0.0));");
            int dirVPosId = idCounter++; sb.AppendLine($"#{dirVPosId} = DIRECTION('', ({-sin:F6}, {cos:F6}, 0.0));");

            int dirUNegId = idCounter++; sb.AppendLine($"#{dirUNegId} = DIRECTION('', ({-cos:F6}, {-sin:F6}, 0.0));");
            int dirUPosId = idCounter++; sb.AppendLine($"#{dirUPosId} = DIRECTION('', ({cos:F6}, {sin:F6}, 0.0));");

            int axisBottom = idCounter++; sb.AppendLine($"#{axisBottom} = AXIS2_PLACEMENT_3D('', #{firstPtId + 0}, #{dirWNegId}, #{dirUId});");
            int planeBottom = idCounter++; sb.AppendLine($"#{planeBottom} = PLANE('', #{axisBottom});");
            int faceBottom = idCounter++; sb.AppendLine($"#{faceBottom} = ADVANCED_FACE('{label}_Bottom', (#{bndBottom}), #{planeBottom}, .F.);");

            int axisTop = idCounter++; sb.AppendLine($"#{axisTop} = AXIS2_PLACEMENT_3D('', #{firstPtId + 4}, #{dirWPosId}, #{dirUId});");
            int planeTop = idCounter++; sb.AppendLine($"#{planeTop} = PLANE('', #{axisTop});");
            int faceTop = idCounter++; sb.AppendLine($"#{faceTop} = ADVANCED_FACE('{label}_Top', (#{bndTop}), #{planeTop}, .T.);");

            int axisFront = idCounter++; sb.AppendLine($"#{axisFront} = AXIS2_PLACEMENT_3D('', #{firstPtId + 0}, #{dirVNegId}, #{dirUId});");
            int planeFront = idCounter++; sb.AppendLine($"#{planeFront} = PLANE('', #{axisFront});");
            int faceFront = idCounter++; sb.AppendLine($"#{faceFront} = ADVANCED_FACE('{label}_Front', (#{bndFront}), #{planeFront}, .T.);");

            int axisBack = idCounter++; sb.AppendLine($"#{axisBack} = AXIS2_PLACEMENT_3D('', #{firstPtId + 3}, #{dirVPosId}, #{dirUId});");
            int planeBack = idCounter++; sb.AppendLine($"#{planeBack} = PLANE('', #{axisBack});");
            int faceBack = idCounter++; sb.AppendLine($"#{faceBack} = ADVANCED_FACE('{label}_Back', (#{bndBack}), #{planeBack}, .T.);");

            int axisLeft = idCounter++; sb.AppendLine($"#{axisLeft} = AXIS2_PLACEMENT_3D('', #{firstPtId + 0}, #{dirUNegId}, #{dirVId});");
            int planeLeft = idCounter++; sb.AppendLine($"#{planeLeft} = PLANE('', #{axisLeft});");
            int faceLeft = idCounter++; sb.AppendLine($"#{faceLeft} = ADVANCED_FACE('{label}_Left', (#{bndLeft}), #{planeLeft}, .T.);");

            int axisRight = idCounter++; sb.AppendLine($"#{axisRight} = AXIS2_PLACEMENT_3D('', #{firstPtId + 1}, #{dirUPosId}, #{dirVId});");
            int planeRight = idCounter++; sb.AppendLine($"#{planeRight} = PLANE('', #{axisRight});");
            int faceRight = idCounter++; sb.AppendLine($"#{faceRight} = ADVANCED_FACE('{label}_Right', (#{bndRight}), #{planeRight}, .T.);");

            // Closed shell containing all 6 faces
            int shellId = idCounter++;
            sb.AppendLine($"#{shellId} = CLOSED_SHELL('', (#{faceBottom}, #{faceTop}, #{faceFront}, #{faceBack}, #{faceLeft}, #{faceRight}));");

            // Manifold solid B-rep
            int solidId = idCounter++;
            sb.AppendLine($"#{solidId} = MANIFOLD_SOLID_BREP('{label}_Solid', #{shellId});");

            return solidId;
        }
    }
}
