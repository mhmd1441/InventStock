"use client";

import Link from "next/link";
import { formatDateTime, formatSignedQuantity } from "@/lib/format";
import type { MovementType, StockMovement } from "@/lib/types";
import { Badge, Table, Td, Th } from "./ui";

const movementTone: Record<MovementType, "success" | "danger" | "warning"> = {
  In: "success",
  Out: "danger",
  Adjustment: "warning",
};

export function MovementTable({
  movements,
  showPart = false,
}: {
  movements: StockMovement[];
  showPart?: boolean;
}) {
  return (
    <Table>
      <thead>
        <tr>
          <Th>Type</Th>
          {showPart && <Th>Part</Th>}
          <Th className="text-right">Change</Th>
          <Th>Reason</Th>
          <Th>By</Th>
          <Th>When</Th>
        </tr>
      </thead>
      <tbody>
        {movements.map((movement) => (
          <tr key={movement.id}>
            <Td>
              <Badge tone={movementTone[movement.movementType]}>
                {movement.movementType}
              </Badge>
            </Td>
            {showPart && (
              <Td>
                <Link
                  href={`/parts/${movement.partId}`}
                  className="font-medium hover:underline"
                >
                  {movement.partNumber}
                </Link>
                <span className="block text-xs text-muted">
                  {movement.partName}
                </span>
              </Td>
            )}
            <Td
              className={`text-right font-medium tabular-nums ${
                movement.quantityChange >= 0 ? "text-success" : "text-danger"
              }`}
            >
              {formatSignedQuantity(movement.quantityChange)}
            </Td>
            <Td className="text-muted">
              {movement.reason ?? "—"}
              {movement.reference && (
                <span className="block text-xs">Ref: {movement.reference}</span>
              )}
            </Td>
            <Td className="text-muted">{movement.performedByUserName}</Td>
            <Td className="whitespace-nowrap text-muted">
              {formatDateTime(movement.occurredAtUtc)}
            </Td>
          </tr>
        ))}
      </tbody>
    </Table>
  );
}
