import { useEffect, useState, type ChangeEvent, type FormEvent } from "react";
import { Button } from "@/components/ui/Button";
import { ListPageShell } from "@/components/ui/ListPageShell";
import { NoticeBanner } from "@/components/ui/NoticeBanner";
import { LoadingBlock } from "@/components/ui/LoadingBlock";
import { FormField, FormError } from "@/components/ui/FormField";
import { SelectField } from "@/components/ui/SelectField";
import { ConfirmDialog, Dialog } from "@/components/ui/Dialog";
import { DataTablePanel } from "@/components/ui/DataTablePanel";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { resolveUploadSrc } from "@/components/ui/imageUploadTypes";
import { Column } from "primereact/column";
import { EntityView, ViewSwitcher, useEntityViewMode } from "@/components/ui/EntityView";

import { useNavigate } from "react-router-dom";
import { Can } from "@/components/auth/Can";
import {
  errorMessage,
  useCreateMutation,
  useDeleteMutation,
  useListQuery,
  usePagedQuery,
  useUpdateMutation,
  mutationErrorMessage,
} from "@/hooks/queries";
import { useFormErrors } from "@/hooks/useFormErrors";
import {
  INVENTORY_PAGE_SIZE,
  createDevice,
  deleteDevice,
  importDevices,
  listDevices,
  listLocations,
  listSwitchPorts,
  updateDevice,
  type DeviceItem,
  type ImportDevicesResult,
  type LocationItem,
  type SwitchPortItem,
} from "@/services/api/inventory";
import { uploadImage } from "@/services/api/uploads";

/** Upload reutilizável — foto do ativo no módulo `devices` do store. */
async function uploadDeviceImage(file: File): Promise<string> {
  const result = await uploadImage(file, "devices");
  if (!result?.imagePath) throw new Error("Upload sem path.");
  return result.imagePath;
}

/**
 * F17-T007 — DevicesPage conectada ao backend (substitui `allDevices`,
 * `sectorMap` e `switchPortMap` de `data/deviceData`). Contrato real:
 * `GET /inventory/devices` (`DeviceDto`, filtro real `type`/`term`, teto de
 * 100/página) + `GET /inventory/locations` (resolve o `locationId` = "setor")
 * + `GET /network/switches/{id}/ports` (`SwitchPortDto`, `linkUp` tri-estado).
 *
 * Sem contrato (saíram): status online/atenção/offline (não existe endpoint
 * de estado atual — histórico de polls é por device), uptime, firmware,
 * "VLAN n" (o DTO traz `vlanId` Guid, sem número/nome), switch de uplink,
 * storage, PoE/speed/VLAN por porta e a lista de clientes (domínio
 * separado). Entraram: vendor/serial no card e contagem real "ativas/total"
 * das portas (`linkUp === true`).
 *
 * Filtro por tipo é client-side sobre a página carregada (os contadores de
 * cada tipo exigiriam N pedidos; o parâmetro `type` do endpoint fica
 * documentado como caminho de escala). Portas dos switches só são buscadas
 * quando a aba "Por Switch" é aberta (1 pedido por switch — sem endpoint
 * agregado).
 */

const typeIcons: Record<string, string> = {
  Server: "pi pi-desktop",
  Workstation: "pi pi-desktop",
  Switch: "pi pi-server",
  Router: "pi pi-sitemap",
  Firewall: "pi pi-shield",
  AccessPoint: "pi pi-wifi",
  Printer: "pi pi-print",
  Camera: "pi pi-video",
  Phone: "pi pi-phone",
  UPS: "pi pi-bolt",
  Other: "pi pi-circle",
};
const typeColors: Record<string, string> = {
  Server: "#3b82f6",
  Workstation: "#8b5cf6",
  Switch: "#8b5cf6",
  Router: "#3b82f6",
  Firewall: "#ec4899",
  AccessPoint: "#22c55e",
  Printer: "#f59e0b",
  Camera: "#ec4899",
  Phone: "#06b6d4",
  UPS: "#f59e0b",
  Other: "#94a3b8",
};
/** Rótulos pt-BR para os nomes do enum `DeviceType` (PascalCase no JSON). */
const typeNames: Record<string, string> = {
  Server: "Servidor",
  Workstation: "Estação de Trabalho",
  Switch: "Switch",
  Router: "Roteador",
  Firewall: "Firewall",
  AccessPoint: "Access Point",
  Printer: "Impressora",
  Camera: "Câmera",
  Phone: "Telefone",
  UPS: "UPS",
  Other: "Outro",
};
const DEVICE_TYPES = Object.keys(typeNames);

/**
 * F34-T006 (ADR-060) — SO informado do equipamento. Só aparece para os
 * tipos com acesso remoto (espelha o guard em Device.Update): com SO
 * registrado a ficha oferece "Acesso remoto" pelo gateway (F34-T007).
 */
const OPERATING_SYSTEM_NAMES: Record<string, string> = {
  Windows: "Windows",
  Linux: "Linux",
  Other: "Outro",
};
const OS_ELIGIBLE_TYPES = new Set([
  "Server",
  "Workstation",
  "VirtualMachine",
  "Router",
  "Firewall",
]);

/** Rótulos do relatório da importação (F50-T008). */
const IMPORT_STATUS_NAMES: Record<string, string> = {
  created: "Criado",
  skipped: "Pulado",
  error: "Erro",
};

export function DevicesPage() {
  const navigate = useNavigate();
  const [view, setView] = useEntityViewMode("devices", "grid");

  const devices = usePagedQuery<DeviceItem>(["devices"], () => listDevices());
  const locationsQuery = useListQuery<LocationItem>(["locations"], () =>
    listLocations().then((page) => page.items),
  );
  const locations = locationsQuery.items;

  const [portsBySwitch, setPortsBySwitch] = useState<Map<string, SwitchPortItem[]>>(new Map());
  const [portsLoading, setPortsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [tab, setTab] = useState<"inventory" | "sector" | "switch">("inventory");
  const [filter, setFilter] = useState("all");

  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<DeviceItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<DeviceItem | null>(null);
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [locationId, setLocationId] = useState("");
  const [ip, setIp] = useState("");
  const [model, setModel] = useState("");
  const [vendor, setVendor] = useState("");
  const [description, setDescription] = useState("");
  const [operatingSystem, setOperatingSystem] = useState("");
  const [imagePath, setImagePath] = useState<string | null>(null);
  // F50-T008 — importação em massa por CSV (dialog com o formato visível).
  const [importOpen, setImportOpen] = useState(false);
  const [importCsv, setImportCsv] = useState("");
  const [importLocationId, setImportLocationId] = useState("");
  const [importReport, setImportReport] = useState<ImportDevicesResult | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  // F29-T001C-2 — as três validações aqui são de TRÊS campos diferentes
  // (nome, tipo, localização), e esta página era a única do recorte sem um
  // sequer `FormField`: os recados saíam todos no mesmo `<div>` no pé do
  // form. Cada validação declara o próprio campo; só a recusa do servidor
  // (catch) fica no formulário, porque o backend não sabe qual campo o
  // usuário pretendeu.
  const {
    error: formError,
    field,
    reset: resetFormError,
    fail: failForm,
    errorFor,
  } = useFormErrors();

  const createMutation = useCreateMutation({
    invalidate: ["devices"],
    mutationFn: (input: Parameters<typeof createDevice>[0]) => createDevice(input),
    onSuccess: (_data, vars) => {
      setNotice(`Dispositivo "${vars.name}" criado.`);
      setActionError(null);
    },
  });
  const updateMutation = useUpdateMutation({
    invalidate: ["devices"],
    mutationFn: (input: { id: string } & Parameters<typeof updateDevice>[1]) => {
      const { id, ...rest } = input;
      return updateDevice(id, rest);
    },
    onSuccess: () => {
      setNotice("Dispositivo atualizado.");
      setActionError(null);
    },
  });
  const deleteMutation = useDeleteMutation({
    invalidate: ["devices"],
    mutationFn: (id: string) => deleteDevice(id),
    onSuccess: () => {
      setNotice("Dispositivo excluído.");
      setActionError(null);
    },
    onError: (err) => setActionError(mutationErrorMessage(err)),
  });

  const loading = devices.isLoading || locationsQuery.isLoading;
  const loadingDevices = devices.isLoading;

  // Portas por switch: só quando a aba "Por Switch" é aberta (1 pedido por
  // switch; `cancelled` evita estado obsoleto se a aba mudar no meio).
  useEffect(() => {
    if (tab !== "switch") return;
    const switches = devices.items.filter((device) => device.type === "Switch");
    if (switches.length === 0) {
      setPortsBySwitch(new Map());
      return;
    }
    let cancelled = false;
    setPortsLoading(true);
    Promise.all(
      switches.map(
        async (device) =>
          [device.id, (await listSwitchPorts(device.id)).items] as const,
      ),
    )
      .then((entries) => {
        if (!cancelled) setPortsBySwitch(new Map(entries));
      })
      .catch((caught: unknown) => {
        if (!cancelled) setError(errorMessage(caught));
      })
      .finally(() => {
        if (!cancelled) setPortsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tab, devices.items]);

  function openCreate() {
    setEditing(null);
    setName("");
    setType("");
    setLocationId("");
    setIp("");
    setModel("");
    setVendor("");
    setDescription("");
    setOperatingSystem("");
    setImagePath(null);
    resetFormError();
    setCreating(true);
  }

  function openEdit(device: DeviceItem) {
    setCreating(false);
    setEditing(device);
    setName(device.name);
    setType(device.type);
    setLocationId(device.locationId);
    setIp(device.ip ?? "");
    setModel(device.model ?? "");
    setVendor(device.vendor ?? "");
    setDescription(device.description ?? "");
    setOperatingSystem(device.operatingSystem ?? "");
    setImagePath(device.imagePath ?? null);
    resetFormError();
    setCreating(true);
  }

  async function submitForm(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      failForm("Informe o nome do dispositivo.", "name");
      return;
    }
    if (!type) {
      failForm("Selecione o tipo do dispositivo.", "type");
      return;
    }
    if (!editing && !locationId) {
      // Só na criação: no edit a localização é travada pelo PUT.
      failForm("Selecione a localização do dispositivo.", "locationId");
      return;
    }
    resetFormError();
    // SO só viaja para tipos elegíveis (espelha o guard do domínio — evita
    // 400 quando o usuário seleciona SO e troca o tipo para um incompatível).
    const operatingSystemPayload =
      operatingSystem && OS_ELIGIBLE_TYPES.has(type) ? operatingSystem : undefined;
    try {
      if (editing) {
        await updateMutation.mutateAsync({
          id: editing.id,
          name: trimmedName,
          type,
          ip: ip.trim() || undefined,
          model: model.trim() || undefined,
          vendor: vendor.trim() || undefined,
          description: description.trim() || undefined,
          operatingSystem: operatingSystemPayload,
          imagePath,
        });
      } else {
        await createMutation.mutateAsync({
          locationId,
          name: trimmedName,
          type,
          ip: ip.trim() || undefined,
          model: model.trim() || undefined,
          vendor: vendor.trim() || undefined,
          description: description.trim() || undefined,
          operatingSystem: operatingSystemPayload,
          imagePath,
        });
      }
      setCreating(false);
      setEditing(null);
    } catch (caught) {
      // Recusa do servidor não aponta campo. `errorMessage` aceita `unknown`
      // (o `catch` devolve isso); `mutationErrorMessage` exigiria `Error |
      // null` e devolveria `string | null`, que o `fail` não aceita.
      failForm(errorMessage(caught));
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;
    try {
      await deleteMutation.mutateAsync(deleteTarget.id);
      setDeleteTarget(null);
      setActionError(null);
    } catch (caught) {
      setActionError(mutationErrorMessage(caught as Error));
    }
  }

  // ── F50-T008 — importação em massa por CSV ─────────────────────────────

  function openImport() {
    setImportCsv("");
    setImportLocationId("");
    setImportReport(null);
    setImportError(null);
    setImporting(false);
    setImportOpen(true);
  }

  /** Arquivo .csv → textarea (mesmo texto que vai para o backend). */
  function onImportFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      setImportCsv(String(reader.result ?? ""));
      setImportReport(null);
      setImportError(null);
    };
    reader.onerror = () => setImportError("Não foi possível ler o arquivo.");
    reader.readAsText(file, "utf-8");
    // Permite escolher o mesmo arquivo de novo após um ajuste.
    event.target.value = "";
  }

  /** Modelo pronto para o Excel (BOM p/ acentos + `;` do Excel pt-BR). */
  function downloadImportTemplate() {
    const content = [
      "nome;ip;tipo;vendor;modelo;mac;serial;descricao",
      "Servidor Web-01;10.0.0.10;Server;Dell;PowerEdge R640;aa:bb:cc:dd:ee:01;SRV001;Servidor de aplicação",
      "Impressora RH;10.0.0.50;Printer;HP;LaserJet Pro;;IMP001;Toner 78%",
      "Câmera Portaria;10.0.0.80;Camera;Intelbras;ICIP-V2126-X24D;;CAM001;POE",
    ].join("\n");
    const blob = new Blob(["\uFEFF", content], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "modelo-importacao-devices.csv";
    anchor.click();
    URL.revokeObjectURL(url);
  }

  async function submitImport() {
    if (!importLocationId) {
      setImportError("Selecione a localização de destino.");
      return;
    }
    if (!importCsv.trim()) {
      setImportError("Cole o conteúdo CSV ou escolha um arquivo .csv.");
      return;
    }
    setImporting(true);
    setImportError(null);
    setImportReport(null);
    try {
      const report = await importDevices({
        locationId: importLocationId,
        csv: importCsv,
      });
      setImportReport(report);
      if (report.created > 0) void devices.refetch();
    } catch (caught) {
      setImportError(errorMessage(caught));
    } finally {
      setImporting(false);
    }
  }

  const items = devices.items;
  const totalCount = devices.totalCount;
  const locationName = (id: string): string =>
    locations.find((location) => location.id === id)?.name ?? "Sem localização";

  const typeCounts: Record<string, number> = {};
  items.forEach((device) => {
    typeCounts[device.type] = (typeCounts[device.type] ?? 0) + 1;
  });
  const presentTypes = DEVICE_TYPES.filter((deviceType) => (typeCounts[deviceType] ?? 0) > 0);

  const filtered = filter === "all" ? items : items.filter((device) => device.type === filter);
  const switches = items.filter((device) => device.type === "Switch");

  return (
    <ListPageShell
      title="Dispositivos"
      singular="dispositivo"
      plural="dispositivos"
      total={totalCount}
      loading={loading}
      note={items.length < totalCount ? `mostrando ${items.length} (refine por tipo)` : undefined}
      notice={notice ? <NoticeBanner message={notice} onDismiss={() => setNotice(null)} /> : undefined}
      error={
        actionError || error || devices.error || locationsQuery.error
          ? (actionError ??
            error ??
            mutationErrorMessage(devices.error as Error | null) ??
            mutationErrorMessage(locationsQuery.error as Error | null) ??
            "Não foi possível falar com o servidor. Tente novamente.")
          : null
      }
      onRetry={() => {
        setActionError(null);
        setError(null);
        void devices.refetch();
        void locationsQuery.refetch();
      }}
      actions={
        <div className="flex align-items-center gap-2 flex-wrap">
          {tab === "inventory" && <ViewSwitcher mode={view} onChange={setView} />}
          <Can level="operate">
            <Button
              variant="primary"
              onClick={openCreate}
              style={{ padding: "6px 14px" }}
            >
              <i className="pi pi-plus" style={{ fontSize: 11 }} /> Adicionar
            </Button>
            <Button
              variant="ghost"
              onClick={openImport}
              style={{ padding: "6px 14px" }}
            >
              <i className="pi pi-upload" style={{ fontSize: 11 }} /> Importar
            </Button>
          </Can>
        </div>
      }
    >

      {creating && (
        <form onSubmit={submitForm} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>{editing ? `Editar — ${editing.name}` : "Novo dispositivo"}</div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 10 }}>
            <FormField label="Nome" error={errorFor("name")}>
              <input
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Nome *"
                autoFocus
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="Tipo" error={errorFor("type")}>
              <SelectField
                value={type}
                onChange={setType}
                placeholder="Tipo *"
                style={{ width: "100%" }}
                options={[
                  { value: "", label: "Tipo *" },
                  ...DEVICE_TYPES.map((deviceType) => ({
                    value: deviceType,
                    label: typeNames[deviceType] ?? deviceType,
                  })),
                ]}
              />
            </FormField>
            <FormField label="Localização" error={errorFor("locationId")}>
              <SelectField
                value={locationId}
                onChange={setLocationId}
                disabled={Boolean(editing)}
                title={editing ? "A localização de um dispositivo não muda pelo PUT" : undefined}
                placeholder="Localização *"
                style={{ width: "100%" }}
                options={[
                  { value: "", label: "Localização *" },
                  ...locations.map((location) => ({
                    value: location.id,
                    label: location.name,
                  })),
                ]}
              />
            </FormField>
            <FormField label="IP" error={null}>
              <input
                value={ip}
                onChange={(event) => setIp(event.target.value)}
                placeholder="IP"
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="Modelo" error={null}>
              <input
                value={model}
                onChange={(event) => setModel(event.target.value)}
                placeholder="Modelo"
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            <FormField label="Fabricante" error={null}>
              <input
                value={vendor}
                onChange={(event) => setVendor(event.target.value)}
                placeholder="Fabricante"
                style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, width: "100%" }}
              />
            </FormField>
            {OS_ELIGIBLE_TYPES.has(type) && (
              <FormField label="Sistema operacional" error={null}>
                <SelectField
                  value={operatingSystem}
                  onChange={setOperatingSystem}
                  placeholder="SO (opcional)"
                  style={{ width: "100%" }}
                  options={[
                    { value: "", label: "SO (opcional)" },
                    ...Object.entries(OPERATING_SYSTEM_NAMES).map(([value, label]) => ({
                      value,
                      label,
                    })),
                  ]}
                />
              </FormField>
            )}
          </div>
          <FormField label="Descrição" error={null}>
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="Descrição (opcional)"
              rows={2}
              style={{ padding: "7px 10px", fontSize: 13, border: "1px solid #d1d5db", borderRadius: 6, resize: "vertical", fontFamily: "inherit", width: "100%" }}
            />
          </FormField>
          <FormField label="Foto do ativo (opcional)" error={null}>
            <ImageUpload
              value={imagePath}
              onChange={setImagePath}
              upload={uploadDeviceImage}
            />
          </FormField>
          <FormError message={field ? "" : (formError ?? "")} />
          <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            <Button
              type="button"
              variant="ghost"
              onClick={() => { setCreating(false); setEditing(null); resetFormError(); }}
              style={{ padding: "6px 14px" }}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={createMutation.isPending || updateMutation.isPending}
              style={{ padding: "6px 14px" }}
            >
              {createMutation.isPending || updateMutation.isPending
                ? "Salvando…"
                : editing ? "Salvar" : "Criar dispositivo"}
            </Button>
          </div>
        </form>
      )}

      <div style={{ display: "flex", gap: 4, borderBottom: "2px solid #f3f4f6", paddingBottom: 0 }}>
        {[
          { key: "inventory" as const, label: "Inventário", icon: "pi pi-list" },
          { key: "sector" as const, label: "Por Setor", icon: "pi pi-map" },
          { key: "switch" as const, label: "Por Switch", icon: "pi pi-server" },
        ].map((entry) => (
          <button
            key={entry.key}
            onClick={() => setTab(entry.key)}
            style={{
              display: "flex", alignItems: "center", gap: 6, padding: "8px 14px", border: "none",
              borderBottom: tab === entry.key ? "2px solid #3b82f6" : "2px solid transparent",
              marginBottom: -2, background: "transparent",
              color: tab === entry.key ? "#3b82f6" : "#6b7280",
              fontSize: 12, fontWeight: tab === entry.key ? 600 : 400, cursor: "pointer",
            }}
          >
            <i className={entry.icon} style={{ fontSize: 12 }} /> {entry.label}
          </button>
        ))}
      </div>

      {tab === "inventory" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <button
              onClick={() => setFilter("all")}
              style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${filter === "all" ? "#3b82f6" : "#e5e7eb"}`, background: filter === "all" ? "#3b82f612" : "white", color: filter === "all" ? "#3b82f6" : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}
            >
              Todos ({items.length})
            </button>
            {presentTypes.map((deviceType) => (
              <button
                key={deviceType}
                onClick={() => setFilter(deviceType)}
                style={{ padding: "5px 10px", borderRadius: 6, border: `1.5px solid ${filter === deviceType ? (typeColors[deviceType] ?? "#94a3b8") : "#e5e7eb"}`, background: filter === deviceType ? `${typeColors[deviceType] ?? "#94a3b8"}12` : "white", color: filter === deviceType ? (typeColors[deviceType] ?? "#94a3b8") : "#6b7280", fontSize: 11, fontWeight: 500, cursor: "pointer" }}
              >
                {typeNames[deviceType] ?? deviceType} ({typeCounts[deviceType]})
              </button>
            ))}
          </div>
          <EntityView<DeviceItem>
            mode={view}
            items={filtered}
            getKey={(device) => device.id}
            emptyMessage={error || actionError ? "—" : "Nenhum dispositivo encontrado"}
            kanban={{
              groupOf: (device) => device.type,
              columns: presentTypes.map((deviceType) => ({
                key: deviceType,
                label: typeNames[deviceType] ?? deviceType,
              })),
              overflowColumnKey: "Other",
            }}
            table={
              <DataTablePanel
                value={filtered}
                dataKey="id"
                loading={loadingDevices}
                emptyMessage={error || actionError ? "—" : "Nenhum dispositivo encontrado"}
              >
                <Column
                  header="Nome"
                  body={(device: DeviceItem) => (
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      {resolveUploadSrc(device.imagePath) ? (
                        <img
                          src={resolveUploadSrc(device.imagePath)!}
                          alt=""
                          style={{ width: 24, height: 24, objectFit: "cover", borderRadius: 4 }}
                        />
                      ) : (
                        <i
                          className={typeIcons[device.type] ?? "pi pi-circle"}
                          style={{ color: typeColors[device.type] ?? "#94a3b8" }}
                        />
                      )}
                      <span style={{ fontWeight: 500 }}>{device.name}</span>
                    </div>
                  )}
                  style={{ minWidth: 200 }}
                />
                <Column header="Tipo" body={(device: DeviceItem) => typeNames[device.type] ?? device.type} style={{ width: 150 }} />
                <Column header="IP" body={(device: DeviceItem) => device.ip ?? "—"} style={{ width: 130 }} />
                <Column header="Localização" body={(device: DeviceItem) => locationName(device.locationId)} style={{ minWidth: 150 }} />
                <Column header="Fabricante" body={(device: DeviceItem) => device.vendor ?? "—"} style={{ width: 130 }} />
                <Column header="Modelo" body={(device: DeviceItem) => device.model ?? "—"} style={{ width: 140 }} />
                <Column header="Serial" body={(device: DeviceItem) => device.serial ?? "—"} style={{ width: 140 }} />
                <Column
                  header=""
                  style={{ width: 150 }}
                  body={(device: DeviceItem) => (
                    <Can level="operate">
                      <div className="flex gap-1">
                        <Button variant="ghost" onClick={() => openEdit(device)}>Editar</Button>
                        <Button variant="ghost" onClick={() => setDeleteTarget(device)}>Excluir</Button>
                      </div>
                    </Can>
                  )}
                />
              </DataTablePanel>
            }
            renderCard={(device) => {
              const color = typeColors[device.type] ?? "#94a3b8";
              return (
                <div className="uni-device-card" onClick={() => navigate(`/devices/${device.id}`)}>
                  {resolveUploadSrc(device.imagePath) && (
                    <img
                      src={resolveUploadSrc(device.imagePath)!}
                      alt={`Foto de ${device.name}`}
                      style={{ width: "100%", height: 120, objectFit: "cover", borderRadius: 6, marginBottom: 8 }}
                    />
                  )}
                  <div className="uni-device-top">
                    <div className={`uni-device-icon ${device.type}`} style={{ background: `${color}15`, color }}>
                      <i className={typeIcons[device.type] ?? "pi pi-circle"} />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="uni-device-name">{device.name}</div>
                      <div className="uni-device-model">{device.model ?? typeNames[device.type] ?? device.type}</div>
                    </div>
                    <span style={{ fontSize: 10, fontWeight: 600, padding: "2px 6px", borderRadius: 4, background: `${color}12`, color, whiteSpace: "nowrap" }}>
                      {typeNames[device.type] ?? device.type}
                    </span>
                  </div>
                  <div className="uni-device-details">
                    <div className="uni-device-detail"><i className="pi pi-desktop" />{device.ip ?? "IP não informado"}</div>
                    <div className="uni-device-detail"><i className="pi pi-map-marker" />{locationName(device.locationId)}</div>
                    {device.vendor && <div className="uni-device-detail"><i className="pi pi-tag" />{device.vendor}</div>}
                  </div>
                  <div className="uni-device-bottom">
                    {device.model && <div className="uni-device-stat"><i className="pi pi-server" />{device.model}</div>}
                    {device.serial && <div className="uni-device-stat"><i className="pi pi-hashtag" />{device.serial}</div>}
                  </div>
                  <Can level="operate">
                    <div
                      className="flex gap-1"
                      style={{ marginTop: 8, paddingTop: 8, borderTop: "1px solid #f3f4f6" }}
                      onClick={(event) => event.stopPropagation()}
                    >
                      <Button variant="ghost" onClick={() => openEdit(device)}>Editar</Button>
                      <Button variant="ghost" onClick={() => setDeleteTarget(device)}>Excluir</Button>
                    </div>
                  </Can>
                </div>
              );
            }}
          />
        </div>
      )}

      {tab === "sector" && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(340px, 1fr))", gap: 12 }}>
          {locations.map((location) => {
            const locationDevices = items.filter((device) => device.locationId === location.id);
            return (
              <div key={location.id} className="uni-panel" style={{ overflow: "hidden" }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 16px", borderBottom: "1px solid #f3f4f6" }}>
                  <div style={{ width: 32, height: 32, borderRadius: 6, background: "#eff6ff", color: "#3b82f6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <i className="pi pi-map-marker" />
                  </div>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 13, fontWeight: 600 }}>{location.name}</div>
                    <div style={{ fontSize: 10, color: "#9ca3af" }}>{location.description ?? "—"}</div>
                  </div>
                  <span style={{ fontSize: 11, color: "#64748b" }}>
                    {locationDevices.length} {locationDevices.length === 1 ? "device" : "devices"}
                  </span>
                </div>
                <div className="uni-panel-body" style={{ padding: "10px 16px" }}>
                  {locationDevices.length === 0 && (
                    <div style={{ fontSize: 11, color: "#9ca3af", textAlign: "center", padding: 8 }}>
                      Nenhum dispositivo neste setor
                    </div>
                  )}
                  <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    {locationDevices.map((device) => {
                      const color = typeColors[device.type] ?? "#94a3b8";
                      return (
                        <div
                          key={device.id}
                          onClick={() => navigate(`/devices/${device.id}`)}
                          style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 8px", borderRadius: 4, cursor: "pointer", background: "#f9fafb", border: "1px solid #f3f4f6" }}
                        >
                          <div style={{ width: 22, height: 22, borderRadius: 4, background: `${color}12`, color, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 10, flexShrink: 0 }}>
                            <i className={typeIcons[device.type] ?? "pi pi-circle"} />
                          </div>
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontSize: 11, fontWeight: 500, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{device.name}</div>
                            <div style={{ fontSize: 9, color: "#9ca3af", fontFamily: "monospace" }}>{device.ip ?? "—"}</div>
                          </div>
                          <span style={{ fontSize: 9, color: "#9ca3af" }}>{typeNames[device.type] ?? device.type}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            );
          })}
          {!loading && locations.length === 0 && (
            <div style={{ gridColumn: "1 / -1", textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
              {error ? "—" : "Nenhuma localização cadastrada"}
            </div>
          )}
          {loading && locations.length === 0 && (
            <LoadingBlock style={{ gridColumn: "1 / -1", background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
          )}
        </div>
      )}

      {tab === "switch" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {switches.map((device) => {
            const ports = portsBySwitch.get(device.id) ?? [];
            const activePorts = ports.filter((port) => port.linkUp === true).length;
            return (
              <div key={device.id} className="uni-panel" style={{ overflow: "hidden" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "12px 16px", borderBottom: "1px solid #f3f4f6", flexWrap: "wrap", gap: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <div style={{ width: 36, height: 36, borderRadius: 8, background: "#8b5cf612", color: "#8b5cf6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                      <i className="pi pi-server" />
                    </div>
                    <div>
                      <div style={{ fontSize: 14, fontWeight: 600 }}>{device.name}</div>
                      <div style={{ fontSize: 11, color: "#9ca3af" }}>
                        {[device.model, locationName(device.locationId)].filter(Boolean).join(" · ")}
                      </div>
                    </div>
                  </div>
                  <div style={{ display: "flex", gap: 12, fontSize: 11 }}>
                    <span style={{ color: "#22c55e", fontWeight: 500 }}>{activePorts} ativas</span>
                    <span style={{ color: "#6b7280" }}>/ {ports.length} portas</span>
                  </div>
                </div>
                <div className="uni-panel-body">
                  {portsLoading && ports.length === 0 && (
                    <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>
                      Carregando portas…
                    </div>
                  )}
                  {!portsLoading && ports.length === 0 && (
                    <div style={{ fontSize: 12, color: "#94a3b8", textAlign: "center", padding: 12 }}>
                      Nenhuma porta cadastrada
                    </div>
                  )}
                  <div className="uni-switch-grid">
                    {ports.map((port) => {
                      const borderColor = port.linkUp === true ? "#22c55e" : port.linkUp === false ? "#ef4444" : "#d1d5db";
                      const bgColor = port.linkUp === true ? "#22c55e08" : port.linkUp === false ? "#fef2f2" : "#f9fafb";
                      return (
                        <div key={port.id} className="uni-switch-port" style={{ borderColor, background: bgColor }}>
                          <div style={{ fontSize: 10, fontWeight: 700, color: port.linkUp === null ? "#9ca3af" : "#111827" }}>{port.name}</div>
                          <div style={{ width: 6, height: 6, borderRadius: "50%", background: borderColor, margin: "2px 0" }} />
                          <div style={{ fontSize: 7, color: "#6b7280" }}>
                            {port.linkUp === true ? "Cima" : port.linkUp === false ? "Baixo" : "Não testada"}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>
            );
          })}
          {!loading && switches.length === 0 && (
            <div style={{ textAlign: "center", padding: 24, color: "#94a3b8", fontSize: 13, background: "white", borderRadius: 8, border: "1px solid #e5e7eb" }}>
              {error ? "—" : "Nenhum switch no inventário"}
            </div>
          )}
          {loading && switches.length === 0 && (
            <LoadingBlock style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 8 }} />
          )}
        </div>
      )}

      {totalCount > items.length && tab !== "switch" && (
        <div style={{ fontSize: 11, color: "#94a3b8", textAlign: "center" }}>
          Exibindo até {INVENTORY_PAGE_SIZE} dispositivos por página (o endpoint não expõe navegação além disso — use os filtros por tipo).
        </div>
      )}

      <Dialog
        visible={importOpen}
        onHide={() => setImportOpen(false)}
        header="Importar devices (CSV)"
        width={680}
        footer={
          <div
            className="flex gap-2"
            style={{ justifyContent: "flex-end", width: "100%" }}
          >
            <Button variant="ghost" onClick={() => setImportOpen(false)}>
              Fechar
            </Button>
            <Button
              variant="primary"
              onClick={() => void submitImport()}
              disabled={importing}
            >
              {importing ? "Importando…" : "Importar arquivo"}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <p style={{ fontSize: 13, color: "#475569", margin: 0 }}>
            Cole o CSV abaixo ou escolha um arquivo. A primeira linha é o
            cabeçalho — <strong>nome</strong> e <strong>tipo</strong> são
            obrigatórios; as demais colunas são opcionais e podem aparecer em
            qualquer ordem. Use <code>;</code> como separador (padrão do Excel
            em português) e não repita o separador dentro dos campos.
          </p>
          <pre
            style={{
              margin: 0,
              padding: 10,
              background: "#0f172a",
              color: "#e2e8f0",
              borderRadius: 8,
              fontSize: 11,
              lineHeight: 1.6,
              overflowX: "auto",
            }}
          >
{`nome;ip;tipo;vendor;modelo;mac;serial;descricao
Servidor Web-01;10.0.0.10;Server;Dell;PowerEdge R640;aa:bb:cc:dd:ee:01;SRV001;Servidor de aplicação
Impressora RH;10.0.0.50;Printer;HP;LaserJet Pro;;IMP001;Toner 78%`}
          </pre>
          <div style={{ fontSize: 12, color: "#64748b" }}>
            Tipos válidos: Server, Workstation, Switch, Router, Firewall,
            AccessPoint, Printer, Camera, Phone, UPS, Other. Nome já existente
            é pulado (re-importar o mesmo arquivo é seguro); o device novo
            nasce com os checks padrão do tipo.
          </div>
          <div className="flex gap-2 flex-wrap">
            <Button variant="ghost" onClick={downloadImportTemplate}>
              <i className="pi pi-download" style={{ fontSize: 11 }} /> Baixar
              modelo
            </Button>
            <label
              className="uni-btn uni-btn-ghost"
              style={{ cursor: "pointer" }}
            >
              <i className="pi pi-file" style={{ fontSize: 11 }} /> Escolher
              arquivo .csv
              <input
                type="file"
                accept=".csv,text/csv"
                onChange={onImportFile}
                style={{ display: "none" }}
              />
            </label>
          </div>
          <FormField label="Conteúdo CSV">
            <textarea
              value={importCsv}
              onChange={(event) => {
                setImportCsv(event.target.value);
                setImportReport(null);
                setImportError(null);
              }}
              placeholder="Cole o conteúdo CSV aqui…"
              rows={5}
              style={{
                width: "100%",
                fontFamily: "monospace",
                fontSize: 12,
                padding: 8,
                border: "1px solid #e5e7eb",
                borderRadius: 6,
                resize: "vertical",
              }}
            />
          </FormField>
          <FormField label="Localização">
            <SelectField
              value={importLocationId}
              onChange={setImportLocationId}
              placeholder="Localização *"
              style={{ width: "100%" }}
              options={[
                { value: "", label: "Localização *" },
                ...locations.map((location) => ({
                  value: location.id,
                  label: location.name,
                })),
              ]}
            />
          </FormField>
          {importError && <FormError message={importError} />}
          {importReport && (
            <div
              style={{
                border: "1px solid #e5e7eb",
                borderRadius: 8,
                padding: 10,
                background: "#f8fafc",
                display: "flex",
                flexDirection: "column",
                gap: 6,
              }}
            >
              <div style={{ fontSize: 13 }}>
                <strong>{importReport.created} criado(s)</strong>
                {" · "}
                <span>{importReport.skipped} pulado(s)</span>
                {" · "}
                <span>{importReport.failed} falha(s)</span>
              </div>
              <div style={{ overflowX: "auto" }}>
                <table
                  style={{
                    width: "100%",
                    fontSize: 12,
                    borderCollapse: "collapse",
                  }}
                >
                  <thead>
                    <tr>
                      {["Linha", "Nome", "Status", "Mensagem"].map((label) => (
                        <th
                          key={label}
                          style={{
                            textAlign: "left",
                            padding: "4px 6px",
                            borderBottom: "1px solid #e5e7eb",
                            color: "#64748b",
                            fontWeight: 600,
                          }}
                        >
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {importReport.rows.map((row) => (
                      <tr key={`${row.line}-${row.name}`}>
                        <td style={{ padding: "4px 6px", borderBottom: "1px solid #f1f5f9" }}>
                          {row.line}
                        </td>
                        <td style={{ padding: "4px 6px", borderBottom: "1px solid #f1f5f9" }}>
                          {row.name || "—"}
                        </td>
                        <td style={{ padding: "4px 6px", borderBottom: "1px solid #f1f5f9" }}>
                          {IMPORT_STATUS_NAMES[row.status] ?? row.status}
                        </td>
                        <td
                          style={{
                            padding: "4px 6px",
                            borderBottom: "1px solid #f1f5f9",
                            color: "#64748b",
                          }}
                        >
                          {row.message}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </Dialog>

      <ConfirmDialog
        visible={deleteTarget !== null}
        onHide={() => setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        header="Excluir dispositivo"
        message={
          deleteTarget
            ? `Excluir "${deleteTarget.name}"? Vínculos ativos (racks/monitoramento) bloqueiam a exclusão com 409.`
            : ""
        }
        confirmLabel="Excluir"
      />
    </ListPageShell>
  );
}
