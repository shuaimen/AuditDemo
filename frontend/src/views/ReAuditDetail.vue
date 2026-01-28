<template>
  <div class="card" v-if="reAudit">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap">
      <div>
        <router-link :to="'/audits/' + reAudit.fromAuditId + '/reaudits'">← 返回复评列表</router-link>
        <div style="font-size:20px;font-weight:700;margin-top:6px">复评单</div>
        <div style="color:#666;font-size:13px;margin-top:6px">状态：{{ reAudit.statusText }}
          <span v-if="reAudit.isPassed!==null">；结果：{{ reAudit.isPassed ? '通过' : '未通过' }}</span>
        </div>
      </div>
      <div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap">
        <button class="secondary" @click="load">刷新</button>
        <button class="secondary" @click="submit" :disabled="reAudit.status!==1">提交复评</button>
        <button class="secondary" @click="close" :disabled="reAudit.status!==2">关闭复评</button>
      </div>
    </div>

    <div v-if="err" class="bad" style="margin-top:10px">{{ err }}</div>

    <div v-if="reAudit.status===2" style="margin-top:10px">
      <label>关闭结论（必填）</label>
      <textarea v-model="closeConclusion" placeholder="填写复评结论"></textarea>
    </div>
  </div>

  <div class="card" v-if="loading">加载中...</div>

  <div class="card" v-else>
    <div v-if="clauses.length===0" style="color:#666">该评鉴暂无不合格条款，无法创建复评。</div>

    <div v-for="c in clauses" :key="c.clauseCode" class="card" style="background:#fbfbfe">
      <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-start">
        <div style="min-width:120px">
          <div><b>{{ c.clauseCode }}</b></div>
          <div class="badge">等级 {{ c.clauseLevel }}</div>
          <div class="badge" style="margin-top:6px">原结果：{{ statusText(c.prevStatus) }}</div>
        </div>
        <div style="flex:1 1 420px">
          <div style="white-space:pre-wrap">{{ c.content }}</div>
          <div style="color:#666;font-size:13px;margin-top:6px" v-if="c.prevComment">原备注：{{ c.prevComment }}</div>
          <div v-if="c.prevPhotos && c.prevPhotos.length" style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px">
            <a v-for="p in c.prevPhotos" :key="p.photoId" :href="p.url" target="_blank">
              <img :src="p.url" style="width:70px;height:70px;object-fit:cover;border-radius:8px;border:1px solid #ddd;opacity:0.7" />
            </a>
          </div>
        </div>
      </div>

      <div class="row" style="margin-top:10px">
        <div class="col">
          <label>复评结果</label>
          <select v-model.number="c.status" :disabled="reAudit.status!==1">
            <option :value="0">未填写</option>
            <option :value="1">符合</option>
            <option :value="2">部分不符合</option>
            <option :value="3">不符合</option>
            <option :value="4">不适用</option>
          </select>
        </div>
        <div class="col">
          <label>复评描述</label>
          <textarea v-model="c.comment" :disabled="reAudit.status!==1" placeholder="描述复评情况"></textarea>
        </div>
        <div class="col">
          <label>复评照片（最多3张，单张≤10MB）</label>
          <input v-if="reAudit.status===1" type="file" accept="image/*" capture="environment" multiple @change="(e)=>uploadPhotos(c,e)" />
          <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px">
            <a v-for="p in c.photos" :key="p.photoId" :href="p.url" target="_blank">
              <img :src="p.url" style="width:86px;height:86px;object-fit:cover;border-radius:8px;border:1px solid #ddd" />
            </a>
          </div>
        </div>
      </div>

      <div style="margin-top:10px;display:flex;gap:10px;align-items:center">
        <button class="secondary" :disabled="reAudit.status!==1" @click="saveClause(c)">保存</button>
        <span v-if="c._saved" style="color:#2e7d32">已保存</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { http } from '../api/http'

const route = useRoute()
const reAuditId = route.params.reAuditId

const reAudit = ref(null)
const clauses = ref([])
const closeConclusion = ref('')

const loading = ref(false)
const err = ref('')

function statusText(s) {
  switch (s) {
    case 1: return '符合'
    case 2: return '部分不符合'
    case 3: return '不符合'
    case 4: return '不适用'
    default: return '未填写'
  }
}

async function load() {
  err.value = ''
  loading.value = true
  try {
    const r = await http.get(`/api/reaudits/${reAuditId}`)
    reAudit.value = r.data.reAudit
    clauses.value = (r.data.clauses || []).map(x => ({ ...x, _saved: false }))
    if (reAudit.value.closeConclusion) closeConclusion.value = reAudit.value.closeConclusion
  } catch (e) {
    err.value = e?.response?.data?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

async function saveClause(c) {
  err.value = ''
  try {
    await http.put(`/api/reaudits/${reAuditId}/clauses/${encodeURIComponent(c.clauseCode)}`, {
      status: c.status,
      comment: c.comment || null
    })
    c._saved = true
    setTimeout(() => (c._saved = false), 1200)
  } catch (e) {
    err.value = e?.response?.data?.message || '保存失败'
  }
}

async function uploadPhotos(c, e) {
  err.value = ''
  const files = Array.from(e.target.files || [])
  if (!files.length) return
  if ((c.photos?.length || 0) + files.length > 3) {
    err.value = '每个条款最多3张照片'
    e.target.value = ''
    return
  }
  const fd = new FormData()
  for (const f of files) {
    if (f.size > 10 * 1024 * 1024) {
      err.value = '单张照片不能超过10MB'
      e.target.value = ''
      return
    }
    fd.append('files', f)
  }
  try {
    await http.post(`/api/reaudits/${reAuditId}/clauses/${encodeURIComponent(c.clauseCode)}/photos`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    await load()
  } catch (ex) {
    err.value = ex?.response?.data?.message || '上传失败'
  } finally {
    e.target.value = ''
  }
}

async function submit() {
  err.value = ''
  try {
    await http.post(`/api/reaudits/${reAuditId}/submit`)
    await load()
  } catch (e) {
    err.value = e?.response?.data?.message || '提交失败（请确认所有条款已填写）'
  }
}

async function close() {
  err.value = ''
  if (!closeConclusion.value || !closeConclusion.value.trim()) {
    err.value = '请填写关闭结论'
    return
  }
  try {
    await http.post(`/api/reaudits/${reAuditId}/close`, { closeConclusion: closeConclusion.value })
    await load()
  } catch (e) {
    err.value = e?.response?.data?.message || '关闭失败'
  }
}

onMounted(load)
</script>
