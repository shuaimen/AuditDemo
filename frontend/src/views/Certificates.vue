<template>
  <div class="d-flex justify-content-between align-items-center mb-3">
    <div>
      <h5 class="mb-0">证照管理</h5>
      <div class="text-muted small">支持到期预警、附件（图片/PDF≤5）</div>
    </div>
    <button class="btn btn-primary" @click="openCreate">新增证照</button>
  </div>

  <div class="card card-soft shadow-sm">
    <div class="card-body">
      <div class="row g-2 align-items-end">
        <div class="col-12 col-md-4">
          <label class="form-label">工厂搜索</label>
          <input class="form-control" v-model="factoryQuery" @input="loadFactories" placeholder="代工厂名称/缩写..." />
        </div>
        <div class="col-12 col-md-4">
          <label class="form-label">工厂</label>
          <select class="form-select" v-model="filters.factoryId">
            <option value="">全部</option>
            <option v-for="f in factories" :key="f.factoryId || f.id" :value="f.factoryId || f.id">{{ f.factoryName || f.name }}</option>
          </select>
        </div>
        <div class="col-6 col-md-2">
          <label class="form-label">到期天数</label>
          <input class="form-control" type="number" v-model.number="filters.days" />
        </div>
        <div class="col-6 col-md-2 d-flex gap-2">
          <button class="btn btn-outline-secondary w-100" :disabled="busy" @click="load">刷新</button>
          <a class="btn btn-outline-success w-100" :href="exportUrl" target="_blank">导出</a>
        </div>
      </div>

      <div class="text-danger mt-2" v-if="err">{{ err }}</div>

      <div class="table-responsive mt-3">
        <table class="table table-sm align-middle">
          <thead>
            <tr>
              <th>工厂</th>
              <th>证照名称</th>
              <th>编号</th>
              <th>有效期</th>
              <th>状态</th>
              <th style="width:140px"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in items" :key="c.certificateId || c.id">
              <td>{{ c.factoryName }}</td>
              <td>{{ c.certName || c.name }}</td>
              <td>{{ c.certNo || c.no }}</td>
              <td>
                <span :class="isExpiring(c) ? 'text-danger fw-semibold' : ''">{{ c.expireDate || c.expiryDate }}</span>
              </td>
              <td><span class="badge bg-secondary">{{ c.status || (c.isActive===false?'停用':'有效') }}</span></td>
              <td class="text-end">
                <button class="btn btn-sm btn-outline-primary" @click="edit(c)">编辑</button>
              </td>
            </tr>
            <tr v-if="!busy && items.length===0">
              <td colspan="6" class="text-muted text-center">暂无数据</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>

  <!-- Create/Edit Modal -->
  <div class="modal fade" id="certModal" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-lg">
      <div class="modal-content">
        <div class="modal-header">
          <h5 class="modal-title">{{ editing?.id ? '编辑证照' : '新增证照' }}</h5>
          <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
        </div>
        <div class="modal-body">
          <div class="row g-2">
            <div class="col-12 col-md-6">
              <label class="form-label">工厂</label>
              <select class="form-select" v-model="form.factoryId">
                <option value="">--请选择--</option>
                <option v-for="f in factories" :key="f.factoryId || f.id" :value="f.factoryId || f.id">{{ f.factoryName || f.name }}</option>
              </select>
            </div>
            <div class="col-12 col-md-6">
              <label class="form-label">证照名称</label>
              <input class="form-control" v-model="form.certName" />
            </div>
            <div class="col-12 col-md-6">
              <label class="form-label">证照编号</label>
              <input class="form-control" v-model="form.certNo" />
            </div>
            <div class="col-12 col-md-6">
              <label class="form-label">有效期至</label>
              <input class="form-control" type="date" v-model="form.expireDate" />
            </div>
            <div class="col-12">
              <label class="form-label">备注</label>
              <textarea class="form-control" rows="2" v-model="form.remark"></textarea>
            </div>
          </div>

          <hr />
          <div class="d-flex justify-content-between align-items-center">
            <div class="fw-semibold">附件（图片/PDF，最多 5 个，单个≤10MB）</div>
            <input type="file" class="form-control" style="max-width:260px" multiple :accept="accept" @change="onPickFiles" />
          </div>
          <div class="text-muted small mt-1">编辑状态下会直接上传到服务器。</div>

          <div class="d-flex flex-wrap gap-2 mt-2">
            <div v-for="f in files" :key="f.fileId || f.id" class="border rounded p-2">
              <a :href="f.url || f.path" target="_blank">{{ f.fileName || f.name || '附件' }}</a>
            </div>
          </div>

          <div class="text-danger mt-2" v-if="modalErr">{{ modalErr }}</div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-outline-secondary" data-bs-dismiss="modal">取消</button>
          <button class="btn btn-primary" :disabled="saving" @click="save">保存</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { CertificatesApi, FactoriesApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const accept = 'image/*,application/pdf'
const busy = ref(false)
const err = ref('')
const items = ref([])

const factories = ref([])
const factoryQuery = ref('')

const filters = ref({ factoryId: '', days: 60 })

const editing = ref(null)
const form = ref({ factoryId: '', certName: '', certNo: '', expireDate: '', remark: '' })
const files = ref([])
const saving = ref(false)
const modalErr = ref('')

const exportUrl = computed(() => CertificatesApi.exportExpiringUrl(filters.value.days, filters.value.factoryId))

function isExpiring(c) {
  const exp = new Date(c.expireDate || c.expiryDate || '')
  if (!exp.getTime()) return false
  const days = filters.value.days || 60
  const t = Date.now() + days * 24 * 3600 * 1000
  return exp.getTime() <= t
}

async function loadFactories() {
  try {
    const data = await FactoriesApi.list(factoryQuery.value)
    factories.value = Array.isArray(data) ? data : (data.items || [])
  } catch {}
}

async function load() {
  err.value = ''
  busy.value = true
  try {
    const data = await CertificatesApi.list({ factoryId: filters.value.factoryId || undefined, days: filters.value.days || undefined })
    items.value = Array.isArray(data) ? data : (data.items || [])
  } catch (e) {
    err.value = apiErrorMessage(e)
  } finally {
    busy.value = false
  }
}

function openCreate() {
  editing.value = null
  form.value = { factoryId: '', certName: '', certNo: '', expireDate: '', remark: '' }
  files.value = []
  modalErr.value = ''
  const el = document.getElementById('certModal')
  // eslint-disable-next-line no-undef
  const modal = new bootstrap.Modal(el)
  modal.show()
}

function edit(c) {
  editing.value = { id: c.certificateId || c.id }
  form.value = {
    factoryId: c.factoryId || '',
    certName: c.certName || c.name || '',
    certNo: c.certNo || c.no || '',
    expireDate: (c.expireDate || c.expiryDate || '').substring(0,10),
    remark: c.remark || ''
  }
  files.value = c.files || []
  modalErr.value = ''
  const el = document.getElementById('certModal')
  // eslint-disable-next-line no-undef
  const modal = new bootstrap.Modal(el)
  modal.show()
}

async function onPickFiles(e) {
  modalErr.value = ''
  const selected = Array.from(e.target.files || [])
  if (selected.length === 0) return
  if (selected.length + files.value.length > 5) {
    modalErr.value = '最多 5 个附件'
    return
  }
  for (const f of selected) {
    if (f.size > 10 * 1024 * 1024) {
      modalErr.value = '单个文件不能超过 10MB'
      return
    }
  }

  // If not saved yet, just keep in memory (will upload after create)
  if (!editing.value?.id) {
    files.value.push(...selected.map((f) => ({ name: f.name, _local: f })))
    return
  }

  // Upload directly
  try {
    for (let i=0;i<selected.length;i++) {
      const res = await CertificatesApi.uploadFile(editing.value.id, selected[i], files.value.length + i + 1)
      if (Array.isArray(res)) files.value = res
      else files.value.push(res)
    }
  } catch (e2) {
    modalErr.value = apiErrorMessage(e2)
  } finally {
    e.target.value = ''
  }
}

async function save() {
  modalErr.value = ''
  saving.value = true
  try {
    if (!form.value.factoryId) throw new Error('请选择工厂')
    if (!form.value.certName) throw new Error('请输入证照名称')
    if (!form.value.expireDate) throw new Error('请输入有效期')

    let id = editing.value?.id
    if (!id) {
      const created = await CertificatesApi.create(form.value)
      id = created?.certificateId || created?.id
      editing.value = { id }
      // upload local files
      const locals = files.value.filter(x => x._local).map(x => x._local)
      for (let i=0;i<locals.length;i++) {
        await CertificatesApi.uploadFile(id, locals[i], i+1)
      }
    } else {
      await CertificatesApi.update(id, form.value)
    }

    await load()
    // close modal
    const el = document.getElementById('certModal')
    // eslint-disable-next-line no-undef
    bootstrap.Modal.getInstance(el)?.hide()
  } catch (e) {
    modalErr.value = apiErrorMessage(e)
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await loadFactories()
  await load()
})
</script>
