using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace RuntimeUnityEditor.Bepin5.PatchInspector
{
	public class IL
	{
		internal static string DisassembleMethod(MethodBase method)
		{
			try
			{
				var methodBody = method.GetMethodBody();
				if (methodBody == null)
				{
					return "Method has no IL body (abstract, extern, or interface method)";
				}

				var ilBytes = methodBody.GetILAsByteArray();
				if (ilBytes.Length == 0)
				{
					return "No IL code available";
				}

				var sb = new StringBuilder();
				sb.AppendLine($"Method Body Size: {ilBytes.Length} bytes");
				sb.AppendLine($"Max Stack Size: {methodBody.MaxStackSize}");
				sb.AppendLine($"Local Variables: {methodBody.LocalVariables.Count}");
				sb.AppendLine();


				if (methodBody.LocalVariables.Count > 0)
				{
					sb.AppendLine("Local Variables:");
					for (int i = 0; i < methodBody.LocalVariables.Count; i++)
					{
						var local = methodBody.LocalVariables[i];
						sb.AppendLine($"  [{i}] {local.LocalType?.Name ?? "Unknown"}");
					}
					sb.AppendLine();
				}

				sb.AppendLine("IL Instructions:");
				sb.AppendLine("────────────────");

				int offset = 0;
				while (offset < ilBytes.Length)
				{
					var instruction = IL.DisassembleInstruction(ilBytes, ref offset, method);
					sb.AppendLine(instruction);
				}

				return sb.ToString();
			}
			catch (Exception ex)
			{
				return $"Error disassembling method: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
			}
		}

		private static string DisassembleInstruction(byte[] ilBytes, ref int offset, MethodBase method)
		{
			if (offset >= ilBytes.Length)
				return "";
			string offsetStr = $"IL_{offset:X4}:";

			byte opcodeByte = ilBytes[offset++];
			OpCode opcode;

			if (opcodeByte == 0xFE && offset < ilBytes.Length)
			{
				byte secondByte = ilBytes[offset++];
				opcode = GetTwoByteOpCode(secondByte);
			}
			else
			{
				opcode = IL.GetOneByteOpCode(opcodeByte);
			}

			string instruction = $"{offsetStr,-10} {opcode.Name}";


			switch (opcode.OperandType)
			{
				case OperandType.InlineNone:
					break;
				case OperandType.InlineI:
					if (offset + 4 <= ilBytes.Length)
					{
						int value = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" {value}";
						offset += 4;
					}
					break;
				case OperandType.InlineI8:
					if (offset + 8 <= ilBytes.Length)
					{
						long value = BitConverter.ToInt64(ilBytes, offset);
						instruction += $" {value}";
						offset += 8;
					}
					break;
				case OperandType.ShortInlineI:
					if (offset < ilBytes.Length)
					{
						instruction += $" {ilBytes[offset]}";
						offset++;
					}
					break;
				case OperandType.InlineString:
					if (offset + 4 <= ilBytes.Length)
					{
						int token = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" \"{ResolveString(token, method)}\"";
						offset += 4;
					}
					break;
				case OperandType.InlineMethod:
					if (offset + 4 <= ilBytes.Length)
					{
						int token = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" {ResolveMethod(token, method)}";
						offset += 4;
					}
					break;
				case OperandType.InlineField:
					if (offset + 4 <= ilBytes.Length)
					{
						int token = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" {ResolveField(token, method)}";
						offset += 4;
					}
					break;
				case OperandType.InlineType:
					if (offset + 4 <= ilBytes.Length)
					{
						int token = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" {ResolveType(token, method)}";
						offset += 4;
					}
					break;
				case OperandType.InlineTok:
					if (offset + 4 <= ilBytes.Length)
					{
						int token = BitConverter.ToInt32(ilBytes, offset);
						instruction += $" {ResolveToken(token, method)}";
						offset += 4;
					}
					break;
				case OperandType.InlineR:
					if (offset + 8 <= ilBytes.Length)
					{
						double value = BitConverter.ToDouble(ilBytes, offset);
						instruction += $" {value}";
						offset += 8;
					}
					break;
				case OperandType.ShortInlineR:
					if (offset + 4 <= ilBytes.Length)
					{
						float value = BitConverter.ToSingle(ilBytes, offset);
						instruction += $" {value}";
						offset += 4;
					}
					break;
				case OperandType.ShortInlineBrTarget:
					if (offset < ilBytes.Length)
					{
						sbyte target = (sbyte)ilBytes[offset];
						int targetOffset = offset + 1 + target;
						instruction += $" IL_{targetOffset:X4}";
						offset++;
					}
					break;
				case OperandType.InlineBrTarget:
					if (offset + 4 <= ilBytes.Length)
					{
						int target = BitConverter.ToInt32(ilBytes, offset);
						int targetOffset = offset + 4 + target;
						instruction += $" IL_{targetOffset:X4}";
						offset += 4;
					}
					break;
				case OperandType.ShortInlineVar:
					if (offset < ilBytes.Length)
					{
						instruction += $" {ilBytes[offset]}";
						offset++;
					}
					break;
				case OperandType.InlineVar:
					if (offset + 2 <= ilBytes.Length)
					{
						short value = BitConverter.ToInt16(ilBytes, offset);
						instruction += $" {value}";
						offset += 2;
					}
					break;
				default:
					int operandSize = IL.GetOperandSize(opcode.OperandType);
					if (offset + operandSize <= ilBytes.Length)
					{
						if (operandSize == 4 && IsTokenBasedOperand(opcode.OperandType))
						{
							int token = BitConverter.ToInt32(ilBytes, offset);
							instruction += $" {ResolveToken(token, method)}";
						}
						else
						{
							switch (operandSize)
							{
								case 1:
									instruction += $" {ilBytes[offset]}";
									break;
								case 2:
									instruction += $" {BitConverter.ToInt16(ilBytes, offset)}";
									break;
								case 4:
									instruction += $" {BitConverter.ToInt32(ilBytes, offset)}";
									break;
								case 8:
									instruction += $" {BitConverter.ToInt64(ilBytes, offset)}";
									break;
							}
						}
						offset += operandSize;
					}
					break;
			}

			return instruction;
		}
		
		private static bool IsTokenBasedOperand(OperandType operandType)
		{
			return operandType == OperandType.InlineField ||
					operandType == OperandType.InlineMethod ||
					operandType == OperandType.InlineString ||
					operandType == OperandType.InlineType ||
					operandType == OperandType.InlineTok;
		}
		
		private static string ResolveToken(int token, MethodBase method)
		{
			try
			{
				var module = method.Module;
				byte tokenType = (byte)((token & 0xFF000000) >> 24);

				switch (tokenType)
				{
					case 0x70:
						return $"\"{module.ResolveString(token)}\"";
					case 0x0A:
					case 0x06:
						var resolvedMethod = module.ResolveMethod(token);
						return $"{resolvedMethod.DeclaringType?.Name}.{resolvedMethod.Name}";
					case 0x04:
						var field = module.ResolveField(token);
						return $"{field.DeclaringType?.Name}.{field.Name}";
					case 0x01:
					case 0x02:
						var type = module.ResolveType(token);
						return type.FullName ?? type.Name;
					default:

						try
						{
							var m = module.ResolveMethod(token);
							return $"{m.DeclaringType?.Name}.{m.Name}";
						}
						catch
						{
							// ignored
						}
						try
						{
							var f = module.ResolveField(token);
							return $"{f.DeclaringType?.Name}.{f.Name}";
						}
						catch
						{
							// ignored
						}
						try
						{
							var t = module.ResolveType(token);
							return t.FullName ?? t.Name;
						}
						catch
						{
							// ignored
						}
						return $"[token:0x{token:X8}]";
				}
			}
			catch
			{
				return $"[token:0x{token:X8}]";
			}
		}
		
		private static string ResolveString(int token, MethodBase method)
		{
			try
			{
				var module = method.Module;
				return module.ResolveString(token);
			}
			catch (Exception)
			{
				return $"[string token:0x{token:X8}]";
			}
		}

		private static string ResolveMethod(int token, MethodBase method)
		{
			try
			{
				var module = method.Module;
				var resolvedMethod = module.ResolveMethod(token);
				return $"{resolvedMethod.DeclaringType?.Name}.{resolvedMethod.Name}";
			}
			catch (Exception)
			{
				return $"[method token:0x{token:X8}]";
			}
		}

		private static string ResolveField(int token, MethodBase method)
		{
			try
			{
				var module = method.Module;
				var field = module.ResolveField(token);
				return $"{field.DeclaringType?.Name}.{field.Name}";
			}
			catch (Exception)
			{
				return $"[field token:0x{token:X8}]";
			}
		}

		private static string ResolveType(int token, MethodBase method)
		{
			try
			{
				var module = method.Module;
				var type = module.ResolveType(token);
				return type.FullName ?? type.Name;
			}
			catch (Exception)
			{
				return $"[type token:0x{token:X8}]";
			}
		}
		
		private static int GetOperandSize(OperandType operandType)
		{
			switch (operandType)
			{
				case OperandType.InlineNone: return 0;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar: return 1;
				case OperandType.InlineVar: return 2;
				case OperandType.InlineI:
				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineMethod:
				case OperandType.InlineString:
				case OperandType.InlineType:
				case OperandType.InlineTok:
				case OperandType.InlineSwitch:
				case OperandType.ShortInlineR: return 4;
				case OperandType.InlineI8:
				case OperandType.InlineR: return 8;
				default: return 0;
			}
		}

		private static OpCode GetOneByteOpCode(byte value)
		{
			var oneByteOpcodes = new Dictionary<byte, OpCode>
			{
				{ 0x00, OpCodes.Nop },
				{ 0x01, OpCodes.Break },
				{ 0x02, OpCodes.Ldarg_0 },
				{ 0x03, OpCodes.Ldarg_1 },
				{ 0x04, OpCodes.Ldarg_2 },
				{ 0x05, OpCodes.Ldarg_3 },
				{ 0x06, OpCodes.Ldloc_0 },
				{ 0x07, OpCodes.Ldloc_1 },
				{ 0x08, OpCodes.Ldloc_2 },
				{ 0x09, OpCodes.Ldloc_3 },
				{ 0x0A, OpCodes.Stloc_0 },
				{ 0x0B, OpCodes.Stloc_1 },
				{ 0x0C, OpCodes.Stloc_2 },
				{ 0x0D, OpCodes.Stloc_3 },
				{ 0x0E, OpCodes.Ldarg_S },
				{ 0x0F, OpCodes.Ldarga_S },
				{ 0x10, OpCodes.Starg_S },
				{ 0x11, OpCodes.Ldloc_S },
				{ 0x12, OpCodes.Ldloca_S },
				{ 0x13, OpCodes.Stloc_S },
				{ 0x14, OpCodes.Ldnull },
				{ 0x15, OpCodes.Ldc_I4_M1 },
				{ 0x16, OpCodes.Ldc_I4_0 },
				{ 0x17, OpCodes.Ldc_I4_1 },
				{ 0x18, OpCodes.Ldc_I4_2 },
				{ 0x19, OpCodes.Ldc_I4_3 },
				{ 0x1A, OpCodes.Ldc_I4_4 },
				{ 0x1B, OpCodes.Ldc_I4_5 },
				{ 0x1C, OpCodes.Ldc_I4_6 },
				{ 0x1D, OpCodes.Ldc_I4_7 },
				{ 0x1E, OpCodes.Ldc_I4_8 },
				{ 0x1F, OpCodes.Ldc_I4_S },
				{ 0x20, OpCodes.Ldc_I4 },
				{ 0x21, OpCodes.Ldc_I8 },
				{ 0x22, OpCodes.Ldc_R4 },
				{ 0x23, OpCodes.Ldc_R8 },
				{ 0x25, OpCodes.Dup },
				{ 0x26, OpCodes.Pop },
				{ 0x27, OpCodes.Jmp },
				{ 0x28, OpCodes.Call },
				{ 0x29, OpCodes.Calli },
				{ 0x2A, OpCodes.Ret },
				{ 0x2B, OpCodes.Br_S },
				{ 0x2C, OpCodes.Brfalse_S },
				{ 0x2D, OpCodes.Brtrue_S },
				{ 0x2E, OpCodes.Beq_S },
				{ 0x2F, OpCodes.Bge_S },
				{ 0x30, OpCodes.Bgt_S },
				{ 0x31, OpCodes.Ble_S },
				{ 0x32, OpCodes.Blt_S },
				{ 0x33, OpCodes.Bne_Un_S },
				{ 0x34, OpCodes.Bge_Un_S },
				{ 0x35, OpCodes.Bgt_Un_S },
				{ 0x36, OpCodes.Ble_Un_S },
				{ 0x37, OpCodes.Blt_Un_S },
				{ 0x38, OpCodes.Br },
				{ 0x39, OpCodes.Brfalse },
				{ 0x3A, OpCodes.Brtrue },
				{ 0x3B, OpCodes.Beq },
				{ 0x3C, OpCodes.Bge },
				{ 0x3D, OpCodes.Bgt },
				{ 0x3E, OpCodes.Ble },
				{ 0x3F, OpCodes.Blt },
				{ 0x40, OpCodes.Bne_Un },
				{ 0x41, OpCodes.Bge_Un },
				{ 0x42, OpCodes.Bgt_Un },
				{ 0x43, OpCodes.Ble_Un },
				{ 0x44, OpCodes.Blt_Un },
				{ 0x45, OpCodes.Switch },
				{ 0x46, OpCodes.Ldind_I1 },
				{ 0x47, OpCodes.Ldind_U1 },
				{ 0x48, OpCodes.Ldind_I2 },
				{ 0x49, OpCodes.Ldind_U2 },
				{ 0x4A, OpCodes.Ldind_I4 },
				{ 0x4B, OpCodes.Ldind_U4 },
				{ 0x4C, OpCodes.Ldind_I8 },
				{ 0x4D, OpCodes.Ldind_I },
				{ 0x4E, OpCodes.Ldind_R4 },
				{ 0x4F, OpCodes.Ldind_R8 },
				{ 0x50, OpCodes.Ldind_Ref },
				{ 0x51, OpCodes.Stind_Ref },
				{ 0x52, OpCodes.Stind_I1 },
				{ 0x53, OpCodes.Stind_I2 },
				{ 0x54, OpCodes.Stind_I4 },
				{ 0x55, OpCodes.Stind_I8 },
				{ 0x56, OpCodes.Stind_R4 },
				{ 0x57, OpCodes.Stind_R8 },
				{ 0x58, OpCodes.Add },
				{ 0x59, OpCodes.Sub },
				{ 0x5A, OpCodes.Mul },
				{ 0x5B, OpCodes.Div },
				{ 0x5C, OpCodes.Div_Un },
				{ 0x5D, OpCodes.Rem },
				{ 0x5E, OpCodes.Rem_Un },
				{ 0x5F, OpCodes.And },
				{ 0x60, OpCodes.Or },
				{ 0x61, OpCodes.Xor },
				{ 0x62, OpCodes.Shl },
				{ 0x63, OpCodes.Shr },
				{ 0x64, OpCodes.Shr_Un },
				{ 0x65, OpCodes.Neg },
				{ 0x66, OpCodes.Not },
				{ 0x67, OpCodes.Conv_I1 },
				{ 0x68, OpCodes.Conv_I2 },
				{ 0x69, OpCodes.Conv_I4 },
				{ 0x6A, OpCodes.Conv_I8 },
				{ 0x6B, OpCodes.Conv_R4 },
				{ 0x6C, OpCodes.Conv_R8 },
				{ 0x6D, OpCodes.Conv_U4 },
				{ 0x6E, OpCodes.Conv_U8 },
				{ 0x6F, OpCodes.Callvirt },
				{ 0x70, OpCodes.Cpobj },
				{ 0x71, OpCodes.Ldobj },
				{ 0x72, OpCodes.Ldstr },
				{ 0x73, OpCodes.Newobj },
				{ 0x74, OpCodes.Castclass },
				{ 0x75, OpCodes.Isinst },
				{ 0x76, OpCodes.Conv_R_Un },
				{ 0x79, OpCodes.Unbox },
				{ 0x7A, OpCodes.Throw },
				{ 0x7B, OpCodes.Ldfld },
				{ 0x7C, OpCodes.Ldflda },
				{ 0x7D, OpCodes.Stfld },
				{ 0x7E, OpCodes.Ldsfld },
				{ 0x7F, OpCodes.Ldsflda },
				{ 0x80, OpCodes.Stsfld },
				{ 0x81, OpCodes.Stobj },
				{ 0x82, OpCodes.Conv_Ovf_I1_Un },
				{ 0x83, OpCodes.Conv_Ovf_I2_Un },
				{ 0x84, OpCodes.Conv_Ovf_I4_Un },
				{ 0x85, OpCodes.Conv_Ovf_I8_Un },
				{ 0x86, OpCodes.Conv_Ovf_U1_Un },
				{ 0x87, OpCodes.Conv_Ovf_U2_Un },
				{ 0x88, OpCodes.Conv_Ovf_U4_Un },
				{ 0x89, OpCodes.Conv_Ovf_U8_Un },
				{ 0x8A, OpCodes.Conv_Ovf_I_Un },
				{ 0x8B, OpCodes.Conv_Ovf_U_Un },
				{ 0x8C, OpCodes.Box },
				{ 0x8D, OpCodes.Newarr },
				{ 0x8E, OpCodes.Ldlen },
				{ 0x8F, OpCodes.Ldelema },
				{ 0x90, OpCodes.Ldelem_I1 },
				{ 0x91, OpCodes.Ldelem_U1 },
				{ 0x92, OpCodes.Ldelem_I2 },
				{ 0x93, OpCodes.Ldelem_U2 },
				{ 0x94, OpCodes.Ldelem_I4 },
				{ 0x95, OpCodes.Ldelem_U4 },
				{ 0x96, OpCodes.Ldelem_I8 },
				{ 0x97, OpCodes.Ldelem_I },
				{ 0x98, OpCodes.Ldelem_R4 },
				{ 0x99, OpCodes.Ldelem_R8 },
				{ 0x9A, OpCodes.Ldelem_Ref },
				{ 0x9B, OpCodes.Stelem_I },
				{ 0x9C, OpCodes.Stelem_I1 },
				{ 0x9D, OpCodes.Stelem_I2 },
				{ 0x9E, OpCodes.Stelem_I4 },
				{ 0x9F, OpCodes.Stelem_I8 },
				{ 0xA0, OpCodes.Stelem_R4 },
				{ 0xA1, OpCodes.Stelem_R8 },
				{ 0xA2, OpCodes.Stelem_Ref },
				{ 0xA3, OpCodes.Ldelem },
				{ 0xA4, OpCodes.Stelem },
				{ 0xA5, OpCodes.Unbox_Any },
				{ 0xB3, OpCodes.Conv_Ovf_I1 },
				{ 0xB4, OpCodes.Conv_Ovf_U1 },
				{ 0xB5, OpCodes.Conv_Ovf_I2 },
				{ 0xB6, OpCodes.Conv_Ovf_U2 },
				{ 0xB7, OpCodes.Conv_Ovf_I4 },
				{ 0xB8, OpCodes.Conv_Ovf_U4 },
				{ 0xB9, OpCodes.Conv_Ovf_I8 },
				{ 0xBA, OpCodes.Conv_Ovf_U8 },
				{ 0xC2, OpCodes.Refanyval },
				{ 0xC3, OpCodes.Ckfinite },
				{ 0xC6, OpCodes.Mkrefany },
				{ 0xD0, OpCodes.Ldtoken },
				{ 0xD1, OpCodes.Conv_U2 },
				{ 0xD2, OpCodes.Conv_U1 },
				{ 0xD3, OpCodes.Conv_I },
				{ 0xD4, OpCodes.Conv_Ovf_I },
				{ 0xD5, OpCodes.Conv_Ovf_U },
				{ 0xD6, OpCodes.Add_Ovf },
				{ 0xD7, OpCodes.Add_Ovf_Un },
				{ 0xD8, OpCodes.Mul_Ovf },
				{ 0xD9, OpCodes.Mul_Ovf_Un },
				{ 0xDA, OpCodes.Sub_Ovf },
				{ 0xDB, OpCodes.Sub_Ovf_Un },
				{ 0xDC, OpCodes.Endfinally },
				{ 0xDD, OpCodes.Leave },
				{ 0xDE, OpCodes.Leave_S },
				{ 0xDF, OpCodes.Stind_I },
				{ 0xE0, OpCodes.Conv_U },
				{ 0xFE, OpCodes.Prefix1 }
			};

			return oneByteOpcodes.ContainsKey(value) ? oneByteOpcodes[value] : OpCodes.Nop;
		}
		
		private static OpCode GetTwoByteOpCode(byte value)
		{
			var twoByteOpcodes = new Dictionary<byte, OpCode>
			{
				{ 0x00, OpCodes.Arglist },
				{ 0x01, OpCodes.Ceq },
				{ 0x02, OpCodes.Cgt },
				{ 0x03, OpCodes.Cgt_Un },
				{ 0x04, OpCodes.Clt },
				{ 0x05, OpCodes.Clt_Un },
				{ 0x06, OpCodes.Ldftn },
				{ 0x07, OpCodes.Ldvirtftn },
				{ 0x09, OpCodes.Ldarg },
				{ 0x0A, OpCodes.Ldarga },
				{ 0x0B, OpCodes.Starg },
				{ 0x0C, OpCodes.Ldloc },
				{ 0x0D, OpCodes.Ldloca },
				{ 0x0E, OpCodes.Stloc },
				{ 0x0F, OpCodes.Localloc },
				{ 0x11, OpCodes.Endfilter },
				{ 0x12, OpCodes.Unaligned },
				{ 0x13, OpCodes.Volatile },
				{ 0x14, OpCodes.Tailcall },
				{ 0x15, OpCodes.Initobj },
				{ 0x16, OpCodes.Constrained },
				{ 0x17, OpCodes.Cpblk },
				{ 0x18, OpCodes.Initblk },
				{ 0x1A, OpCodes.Rethrow },
				{ 0x1C, OpCodes.Sizeof },
				{ 0x1D, OpCodes.Refanytype },
				{ 0x1E, OpCodes.Readonly }
			};

			return twoByteOpcodes.ContainsKey(value) ? twoByteOpcodes[value] : OpCodes.Nop;
		}

	}
}