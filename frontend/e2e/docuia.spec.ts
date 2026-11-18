import { expect, Page, test } from '@playwright/test';

// Un PDF mínimo: lo que importa para la aplicación es su firma («%PDF»); en modo demostración el contenido no se lee.
const PDF = Buffer.from('%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n');

async function entrarComoInvitado(page: Page) {
  await page.goto('/acceso');
  await page.getByRole('button', { name: 'Entrar como invitado' }).click();
  await expect(page.getByRole('heading', { name: 'Mis documentos' })).toBeVisible();
}

async function abrirSubida(page: Page) {
  await page.getByRole('button', { name: /Subir (mi primer )?documento/ }).first().click();
  await expect(page.getByRole('heading', { name: 'Subir documento' })).toBeVisible();
}

test.describe('Acceso', () => {
  test('con el formulario vacío se muestran los errores y no se llama a la API', async ({ page }) => {
    const llamadas: string[] = [];
    page.on('request', r => { if (r.url().includes('Autenticacion')) llamadas.push(r.url()); });

    await page.goto('/acceso');
    await page.getByRole('button', { name: 'Ingresar', exact: true }).click();

    await expect(page.locator('mat-error').first()).toBeVisible();
    expect(llamadas).toEqual([]);
  });

  test('una clave equivocada muestra un mensaje que no revela qué falló', async ({ page }) => {
    await page.goto('/acceso');
    await page.getByLabel('Correo').first().fill('nadie@correo.com');
    await page.getByLabel('Clave').first().fill('ClaveIncorrecta1');
    await page.getByRole('button', { name: 'Ingresar', exact: true }).click();

    await expect(page.getByRole('alert')).toContainText('El correo o la clave no son correctos');
  });

  test('sin sesión las pantallas internas llevan al acceso', async ({ page }) => {
    await page.goto('/documentos');
    await expect(page).toHaveURL(/acceso/);
  });
});

test.describe('Documentos', () => {
  test('se registra una cuenta nueva, sube un PDF, corrige los datos, pregunta y elimina el documento', async ({ page }) => {
    const email = `e2e${Date.now().toString(36)}@prueba.com`;

    // Registro
    await page.goto('/acceso');
    await page.getByRole('tab', { name: 'Crear cuenta' }).click();
    await page.getByPlaceholder('Ej.: Ana Torres').fill('Ana E2E');
    await page.getByPlaceholder('Ej.: ana@correo.com').last().fill(email);
    await page.getByPlaceholder('Mínimo 8 caracteres').fill('Clave12345');
    await page.getByLabel('Repite la clave').fill('Clave12345');
    await page.getByRole('button', { name: 'Crear cuenta' }).last().click();
    await expect(page.getByRole('heading', { name: 'Mis documentos' })).toBeVisible();

    // Subida: sin archivo no se envía
    await abrirSubida(page);
    await page.getByRole('button', { name: 'Analizar documento' }).click();
    await expect(page.getByRole('alert')).toBeVisible();

    // Subida válida
    await page.locator('#archivo').setInputFiles({ name: 'factura-e2e.pdf', mimeType: 'application/pdf', buffer: PDF });
    await expect(page.getByText('factura-e2e.pdf')).toBeVisible();
    await page.getByRole('button', { name: 'Analizar documento' }).click();
    await expect(page.getByRole('dialog')).toContainText('Documento analizado');
    await page.getByRole('dialog').getByRole('button').first().click();

    // Tras analizar, la aplicación abre el detalle con los datos de demostración
    await expect(page.getByRole('heading', { name: 'Datos encontrados' })).toBeVisible();
    const total = page.getByPlaceholder('Ej.: 1180.00');
    await expect(total).toHaveValue('1180.00');

    // Un valor vacío no se puede confirmar
    await total.fill('');
    await page.getByRole('button', { name: 'Confirmar datos' }).click();
    await expect(page.locator('mat-error').first()).toBeVisible();

    await total.fill('1250.00');
    await page.getByRole('button', { name: 'Confirmar datos' }).click();
    await expect(page.getByRole('dialog')).toContainText('Datos confirmados');
    await page.getByRole('dialog').getByRole('button').first().click();

    // Chat con el documento
    await page.getByLabel(/Tu pregunta/).fill('¿Cuál es el total?');
    await page.getByRole('button', { name: 'Preguntar' }).click();
    await expect(page.getByRole('log')).toContainText('¿Cuál es el total?');
    await expect(page.getByRole('log')).toContainText('DocuIA');

    // Eliminar con confirmación
    await page.getByRole('button', { name: 'Eliminar' }).click();
    await expect(page.getByRole('dialog')).toContainText('¿Eliminar el documento?');
    await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
    await expect(page.getByRole('heading', { name: 'Mis documentos' })).toBeVisible();
    await expect(page.getByText('factura-e2e.pdf')).toHaveCount(0);
  });

  test('un Excel o un archivo con la extensión cambiada se rechazan con un mensaje claro', async ({ page }) => {
    await entrarComoInvitado(page);
    await abrirSubida(page);

    await page.locator('#archivo').setInputFiles({ name: 'ventas.xlsx', mimeType: 'application/vnd.ms-excel', buffer: Buffer.from('PK\u0003\u0004 contenido') });
    await expect(page.getByRole('alert')).toContainText('Excel');

    await page.locator('#archivo').setInputFiles({ name: 'falso.png', mimeType: 'image/png', buffer: PDF });
    await expect(page.getByRole('alert')).toContainText(/no corresponde|extensión/i);
    await expect(page.locator('.elegido')).toHaveCount(0);
  });

  test('un invitado no ve los documentos de otra persona', async ({ browser }) => {
    const a = await (await browser.newContext()).newPage();
    await entrarComoInvitado(a);
    await abrirSubida(a);
    await a.locator('#archivo').setInputFiles({ name: 'privado-a.pdf', mimeType: 'application/pdf', buffer: PDF });
    await a.getByRole('button', { name: 'Analizar documento' }).click();
    await expect(a.getByRole('dialog')).toContainText('Documento analizado');

    const b = await (await browser.newContext()).newPage();
    await entrarComoInvitado(b);
    await expect(b.getByText('privado-a.pdf')).toHaveCount(0);
  });
});
