import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { ErrorBanner } from "@/components/ui/ErrorBanner";
import { PageHeader } from "@/components/ui/PageHeader";
import { useAppSelector } from "@/app/store/hooks";
import {
  getCompanySettings,
  saveCompanySettings,
  type CompanySettingsItem,
} from "@/services/api/companySettings";
import { uploadImage } from "@/services/api/uploads";

const INPUT_STYLE = {
  width: "100%",
  padding: "9px 10px",
  borderRadius: 8,
  border: "1px solid #cbd5e1",
  boxSizing: "border-box" as const,
};

interface DeptForm {
  name: string;
  responsible: string;
}

interface FormState {
  name: string;
  legalName: string;
  cnpj: string;
  logoPath: string;
  street: string;
  number: string;
  complement: string;
  district: string;
  city: string;
  state: string;
  zipCode: string;
  phone: string;
  email: string;
  website: string;
  departments: DeptForm[];
}

const EMPTY: FormState = {
  name: "",
  legalName: "",
  cnpj: "",
  logoPath: "",
  street: "",
  number: "",
  complement: "",
  district: "",
  city: "",
  state: "",
  zipCode: "",
  phone: "",
  email: "",
  website: "",
  departments: [],
};

function formOf(item: CompanySettingsItem | null): FormState {
  if (!item) return EMPTY;
  return {
    name: item.name,
    legalName: item.legalName ?? "",
    cnpj: item.cnpj ?? "",
    logoPath: item.logoPath ?? "",
    street: item.street ?? "",
    number: item.number ?? "",
    complement: item.complement ?? "",
    district: item.district ?? "",
    city: item.city ?? "",
    state: item.state ?? "",
    zipCode: item.zipCode ?? "",
    phone: item.phone ?? "",
    email: item.email ?? "",
    website: item.website ?? "",
    departments: item.departments.map((d) => ({
      name: d.name,
      responsible: d.responsible ?? "",
    })),
  };
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label style={{ display: "grid", gap: 4 }}>
      <span style={{ fontSize: 12, color: "#475569" }}>{label}</span>
      {children}
    </label>
  );
}

/**
 * Configuração institucional (spec evolução §3): dados da empresa usados
 * automaticamente nos relatórios gerados. Escrita exclusiva de Admin.
 */
export function CompanySettingsPage() {
  const roles = useAppSelector((state) => state.auth.user?.roles ?? []);
  const isAdmin = roles.includes("Admin");
  const [form, setForm] = useState<FormState>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setForm(formOf(await getCompanySettings()));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar a configuração.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleLogo(file: File | null) {
    if (!file) return;
    setSaving(true);
    setError(null);
    try {
      const result = await uploadImage(file, "general");
      if (result?.imagePath) set("logoPath", result.imagePath);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível enviar o logo.");
    } finally {
      setSaving(false);
    }
  }

  async function handleSave() {
    if (!form.name.trim()) {
      setError("O nome da empresa é obrigatório.");
      return;
    }
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await saveCompanySettings({
        name: form.name.trim(),
        legalName: form.legalName.trim() || null,
        cnpj: form.cnpj.trim() || null,
        logoPath: form.logoPath.trim() || null,
        street: form.street.trim() || null,
        number: form.number.trim() || null,
        complement: form.complement.trim() || null,
        district: form.district.trim() || null,
        city: form.city.trim() || null,
        state: form.state.trim() || null,
        zipCode: form.zipCode.trim() || null,
        phone: form.phone.trim() || null,
        email: form.email.trim() || null,
        website: form.website.trim() || null,
        departments: form.departments
          .filter((d) => d.name.trim())
          .map((d) => ({
            name: d.name.trim(),
            responsible: d.responsible.trim() || null,
          })),
      });
      setNotice("Configuração institucional salva.");
      await load();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível salvar.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <div style={{ padding: 24, color: "#64748b" }}>Carregando…</div>;
  }

  return (
    <div style={{ display: "grid", gap: 16, maxWidth: 960 }}>
      <PageHeader
        title="Configuração institucional"
        subtitle="Dados da empresa utilizados automaticamente nos relatórios gerados."
        actions={
          isAdmin ? (
            <Button variant="primary" onClick={() => void handleSave()} disabled={saving}>
              {saving ? "Salvando…" : "Salvar"}
            </Button>
          ) : undefined
        }
      />

      {!isAdmin && (
        <div style={{ padding: 10, borderRadius: 8, background: "#fef3c7", color: "#b45309", fontSize: 12 }}>
          Somente administradores podem editar a configuração institucional.
        </div>
      )}
      {notice && (
        <div role="status" style={{ padding: 10, borderRadius: 8, background: "#f0fdf4", color: "#15803d", fontSize: 12 }}>
          {notice}
        </div>
      )}
      {error && <ErrorBanner message={error} onRetry={() => void load()} />}

      <fieldset disabled={!isAdmin} style={{ border: "1px solid #e2e8f0", borderRadius: 12, padding: 20, display: "grid", gap: 16, background: "#fff" }}>
        <div style={{ fontWeight: 700, fontSize: 15 }}>Identificação</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 12 }}>
          <Field label="Nome da empresa *">
            <input style={INPUT_STYLE} value={form.name} onChange={(e) => set("name", e.target.value)} />
          </Field>
          <Field label="Razão social">
            <input style={INPUT_STYLE} value={form.legalName} onChange={(e) => set("legalName", e.target.value)} />
          </Field>
          <Field label="CNPJ">
            <input style={INPUT_STYLE} value={form.cnpj} onChange={(e) => set("cnpj", e.target.value)} placeholder="00.000.000/0000-00" />
          </Field>
          <Field label="Logotipo">
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <input
                type="file"
                accept="image/*"
                style={{ fontSize: 12 }}
                onChange={(e) => void handleLogo(e.target.files?.[0] ?? null)}
              />
              {form.logoPath && (
                <span style={{ fontSize: 11, color: "#15803d" }}>Enviado</span>
              )}
            </div>
          </Field>
        </div>

        <div style={{ fontWeight: 700, fontSize: 15 }}>Endereço</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 12 }}>
          <Field label="Logradouro"><input style={INPUT_STYLE} value={form.street} onChange={(e) => set("street", e.target.value)} /></Field>
          <Field label="Número"><input style={INPUT_STYLE} value={form.number} onChange={(e) => set("number", e.target.value)} /></Field>
          <Field label="Complemento"><input style={INPUT_STYLE} value={form.complement} onChange={(e) => set("complement", e.target.value)} /></Field>
          <Field label="Bairro"><input style={INPUT_STYLE} value={form.district} onChange={(e) => set("district", e.target.value)} /></Field>
          <Field label="Cidade"><input style={INPUT_STYLE} value={form.city} onChange={(e) => set("city", e.target.value)} /></Field>
          <Field label="UF"><input style={INPUT_STYLE} value={form.state} onChange={(e) => set("state", e.target.value)} /></Field>
          <Field label="CEP"><input style={INPUT_STYLE} value={form.zipCode} onChange={(e) => set("zipCode", e.target.value)} /></Field>
        </div>

        <div style={{ fontWeight: 700, fontSize: 15 }}>Contato</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
          <Field label="Telefone"><input style={INPUT_STYLE} value={form.phone} onChange={(e) => set("phone", e.target.value)} /></Field>
          <Field label="E-mail"><input style={INPUT_STYLE} type="email" value={form.email} onChange={(e) => set("email", e.target.value)} /></Field>
          <Field label="Site"><input style={INPUT_STYLE} value={form.website} onChange={(e) => set("website", e.target.value)} /></Field>
        </div>

        <div style={{ fontWeight: 700, fontSize: 15 }}>Departamentos e setores</div>
        <div style={{ display: "grid", gap: 8 }}>
          {form.departments.map((dept, index) => (
            <div key={index} style={{ display: "grid", gridTemplateColumns: "2fr 2fr auto", gap: 8, alignItems: "center" }}>
              <input
                style={INPUT_STYLE}
                placeholder="Nome do departamento/setor"
                value={dept.name}
                onChange={(e) => {
                  const departments = [...form.departments];
                  departments[index] = { ...dept, name: e.target.value };
                  set("departments", departments);
                }}
              />
              <input
                style={INPUT_STYLE}
                placeholder="Responsável"
                value={dept.responsible}
                onChange={(e) => {
                  const departments = [...form.departments];
                  departments[index] = { ...dept, responsible: e.target.value };
                  set("departments", departments);
                }}
              />
              <Button
                variant="danger"
                icon="pi pi-trash"
                aria-label="Remover departamento"
                onClick={() => set("departments", form.departments.filter((_, i) => i !== index))}
              />
            </div>
          ))}
          <div>
            <Button
              variant="ghost"
              icon="pi pi-plus"
              onClick={() => set("departments", [...form.departments, { name: "", responsible: "" }])}
            >
              Adicionar departamento
            </Button>
          </div>
        </div>
      </fieldset>
    </div>
  );
}
