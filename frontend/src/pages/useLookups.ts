import { useEffect, useState } from "react";
import { api } from "../api";
import { EntityDef } from "../entities";

export type Row = Record<string, unknown> & { id: number };
export type LookupMap = Record<string, Record<number, string>>;

/** Loads option lists for every lookup field of an entity and returns id->label maps. */
export function useLookups(def: EntityDef) {
  const [lookups, setLookups] = useState<LookupMap>({});

  useEffect(() => {
    const lookupRoutes = Array.from(
      new Set(def.fields.filter((f) => f.type === "lookup" && f.lookup).map((f) => f.lookup!))
    );
    if (lookupRoutes.length === 0) {
      setLookups({});
      return;
    }
    Promise.all(lookupRoutes.map((r) => api.list<Row>(r))).then((results) => {
      const map: LookupMap = {};
      lookupRoutes.forEach((r, i) => {
        map[r] = {};
        for (const row of results[i]) {
          const label = (row["name"] as string) ?? (row["value"] as string) ?? `#${row.id}`;
          map[r][row.id] = label;
        }
      });
      setLookups(map);
    });
  }, [def.route]);

  return lookups;
}
